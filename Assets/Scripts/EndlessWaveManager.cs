using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EndlessWaveManager : MonoBehaviour
{
	private const float MinimumSpawnInterval = 0.01f;

	[Header("Enemy")]
	[SerializeField, Tooltip("The enemy prefab spawned by this manager. It must contain a BodyController in its hierarchy.")]
	private GameObject enemyPrefab;

	[SerializeField, Tooltip("Empty scene objects used as enemy spawn positions and rotations.")]
	private List<Transform> spawnPoints = new List<Transform>();

	[Header("Mode")]
	[SerializeField, Tooltip("When enabled, stop all spawning after the configured number of full waves. When disabled, spawning continues endlessly.")]
	private bool limitWaveCount;

	[SerializeField, Min(1), Tooltip("Number of full waves to spawn, including the immediate opening wave.")]
	private int numberOfWaves = 5;

	[Header("Capacity")]
	[SerializeField, Min(1), Tooltip("Maximum number of living enemies owned by this manager.")]
	private int maxActiveEnemies = 10;

	[SerializeField, Min(1), Tooltip("Number of enemies spawned simultaneously when enough capacity is available.")]
	private int waveSize = 3;

	[Header("Wave Timing")]
	[SerializeField, Min(MinimumSpawnInterval)]
	private float minWaveInterval = 3f;

	[SerializeField, Min(MinimumSpawnInterval)]
	private float maxWaveInterval = 6f;

	[Header("Trickle Timing")]
	[SerializeField, Min(MinimumSpawnInterval)]
	private float minTrickleInterval = 0.5f;

	[SerializeField, Min(MinimumSpawnInterval)]
	private float maxTrickleInterval = 1.5f;

	[Header("Cleanup")]
	[SerializeField, Min(0f), Tooltip("Seconds a dead enemy remains as a ragdoll before being destroyed.")]
	private float corpseLifetime = 5f;

	[Header("Lifecycle")]
	[SerializeField, Tooltip("Begin spawning automatically when this component becomes enabled.")]
	private bool autoStart = true;

	private readonly List<TrackedEnemy> trackedEnemies = new List<TrackedEnemy>();
	private readonly List<Transform> validSpawnPoints = new List<Transform>();
	private readonly List<Transform> waveCandidates = new List<Transform>();
	private Coroutine spawnCoroutine;
	private bool hasSpawnedOpeningWave;

	public int ActiveEnemyCount { get; private set; }
	public int TrackedCorpseCount { get; private set; }
	public int WavesSpawned { get; private set; }
	public bool IsSpawning => spawnCoroutine != null;
	public bool HasReachedWaveLimit => limitWaveCount && WavesSpawned >= numberOfWaves;

	private enum EnemyState
	{
		Alive,
		Corpse
	}

	private sealed class TrackedEnemy
	{
		public GameObject Root;
		public BodyController Body;
		public EnemyState State;
		public float CleanupTime;
	}

	private void OnEnable()
	{
		if (autoStart)
		{
			StartSpawning();
		}
	}

	private void Update()
	{
		UpdateTrackedEnemies();
	}

	private void OnDisable()
	{
		StopSpawning();
	}

	private void OnDestroy()
	{
		for (int i = trackedEnemies.Count - 1; i >= 0; i--)
		{
			ReleaseEnemy(trackedEnemies[i].Root);
		}

		trackedEnemies.Clear();
		ActiveEnemyCount = 0;
		TrackedCorpseCount = 0;
	}

	private void OnValidate()
	{
		numberOfWaves = Mathf.Max(1, numberOfWaves);
		maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
		waveSize = Mathf.Clamp(waveSize, 1, maxActiveEnemies);
		corpseLifetime = Mathf.Max(0f, corpseLifetime);

		NormalizeIntervalRange(ref minWaveInterval, ref maxWaveInterval);
		NormalizeIntervalRange(ref minTrickleInterval, ref maxTrickleInterval);

		int assignedSpawnPointCount = CountAssignedSpawnPoints();
		if (assignedSpawnPointCount > 0)
		{
			waveSize = Mathf.Min(waveSize, assignedSpawnPointCount);
		}
	}

	public void StartSpawning()
	{
		if (spawnCoroutine != null || !isActiveAndEnabled)
		{
			return;
		}

		if (!TryValidateConfiguration(out string validationError))
		{
			Debug.LogError($"{nameof(EndlessWaveManager)} cannot start: {validationError}", this);
			return;
		}

		if (!hasSpawnedOpeningWave && !HasReachedWaveLimit)
		{
			hasSpawnedOpeningWave = true;
			SpawnWaveAndRecord(GetEffectiveWaveSize());
		}

		if (HasReachedWaveLimit)
		{
			return;
		}

		spawnCoroutine = StartCoroutine(SpawnLoop());
	}

	public void StopSpawning()
	{
		if (spawnCoroutine == null)
		{
			return;
		}

		StopCoroutine(spawnCoroutine);
		spawnCoroutine = null;
	}

	private IEnumerator SpawnLoop()
	{
		while (true)
		{
			if (HasReachedWaveLimit)
			{
				spawnCoroutine = null;
				yield break;
			}

			if (ActiveEnemyCount >= maxActiveEnemies)
			{
				yield return null;
				continue;
			}

			RefreshValidSpawnPoints();
			if (validSpawnPoints.Count == 0)
			{
				Debug.LogError($"{nameof(EndlessWaveManager)} stopped because it has no valid spawn points.", this);
				spawnCoroutine = null;
				yield break;
			}

			int capacity = maxActiveEnemies - ActiveEnemyCount;
			int effectiveWaveSize = GetEffectiveWaveSize();
			bool canSpawnWave = capacity >= effectiveWaveSize;
			float delay = canSpawnWave
				? Random.Range(minWaveInterval, maxWaveInterval)
				: Random.Range(minTrickleInterval, maxTrickleInterval);

			yield return new WaitForSeconds(delay);

			// Counts and scene references may have changed during the delay.
			RefreshValidSpawnPoints();
			if (validSpawnPoints.Count == 0)
			{
				Debug.LogError($"{nameof(EndlessWaveManager)} stopped because it has no valid spawn points.", this);
				spawnCoroutine = null;
				yield break;
			}

			capacity = maxActiveEnemies - ActiveEnemyCount;
			if (capacity <= 0)
			{
				continue;
			}

			effectiveWaveSize = GetEffectiveWaveSize();
			if (capacity >= effectiveWaveSize)
			{
				SpawnWaveAndRecord(effectiveWaveSize);
			}
			else
			{
				SpawnTrickle();
			}
		}
	}

	private void SpawnWaveAndRecord(int count)
	{
		SpawnWave(count);
		WavesSpawned++;
	}

	private void SpawnWave(int count)
	{
		waveCandidates.Clear();
		waveCandidates.AddRange(validSpawnPoints);

		int spawnedCount = 0;
		while (spawnedCount < count && waveCandidates.Count > 0 && ActiveEnemyCount < maxActiveEnemies)
		{
			int candidateIndex = Random.Range(0, waveCandidates.Count);
			Transform spawnPoint = waveCandidates[candidateIndex];

			int lastIndex = waveCandidates.Count - 1;
			waveCandidates[candidateIndex] = waveCandidates[lastIndex];
			waveCandidates.RemoveAt(lastIndex);

			if (SpawnEnemy(spawnPoint))
			{
				spawnedCount++;
			}
		}
	}

	private void SpawnTrickle()
	{
		if (validSpawnPoints.Count == 0 || ActiveEnemyCount >= maxActiveEnemies)
		{
			return;
		}

		Transform spawnPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
		SpawnEnemy(spawnPoint);
	}

	private bool SpawnEnemy(Transform spawnPoint)
	{
		if (enemyPrefab == null || spawnPoint == null || ActiveEnemyCount >= maxActiveEnemies)
		{
			return false;
		}

		GameObject enemyRoot = Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
		BodyController body = enemyRoot.GetComponentInChildren<BodyController>(true);
		if (body == null)
		{
			Debug.LogError($"Spawned enemy '{enemyRoot.name}' has no {nameof(BodyController)} in its hierarchy.", enemyRoot);
			ReleaseEnemy(enemyRoot);
			return false;
		}

		trackedEnemies.Add(new TrackedEnemy
		{
			Root = enemyRoot,
			Body = body,
			State = EnemyState.Alive
		});

		ActiveEnemyCount++;
		return true;
	}

	private void UpdateTrackedEnemies()
	{
		for (int i = trackedEnemies.Count - 1; i >= 0; i--)
		{
			TrackedEnemy enemy = trackedEnemies[i];

			if (enemy.Root == null)
			{
				RemoveTrackedEnemyAt(i, enemy.State);
				continue;
			}

			if (enemy.State == EnemyState.Alive)
			{
				if (enemy.Body == null)
				{
					ReleaseEnemy(enemy.Root);
					RemoveTrackedEnemyAt(i, EnemyState.Alive);
					continue;
				}

				if (enemy.Body.isDead)
				{
					enemy.State = EnemyState.Corpse;
					enemy.CleanupTime = Time.time + corpseLifetime;
					ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
					TrackedCorpseCount++;
				}
			}

			if (enemy.State == EnemyState.Corpse && Time.time >= enemy.CleanupTime)
			{
				ReleaseEnemy(enemy.Root);
				RemoveTrackedEnemyAt(i, EnemyState.Corpse);
			}
		}
	}

	private void RemoveTrackedEnemyAt(int index, EnemyState state)
	{
		trackedEnemies.RemoveAt(index);
		if (state == EnemyState.Alive)
		{
			ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
		}
		else
		{
			TrackedCorpseCount = Mathf.Max(0, TrackedCorpseCount - 1);
		}
	}

	private void ReleaseEnemy(GameObject enemyRoot)
	{
		if (enemyRoot != null)
		{
			Destroy(enemyRoot);
		}
	}

	private bool TryValidateConfiguration(out string validationError)
	{
		NormalizeRuntimeValues();

		if (enemyPrefab == null)
		{
			validationError = "no enemy prefab is assigned.";
			return false;
		}

		if (enemyPrefab.GetComponentInChildren<BodyController>(true) == null)
		{
			validationError = $"the enemy prefab must contain a {nameof(BodyController)} in its hierarchy.";
			return false;
		}

		RefreshValidSpawnPoints();
		if (validSpawnPoints.Count == 0)
		{
			validationError = "at least one non-null spawn point is required.";
			return false;
		}

		if (waveSize > validSpawnPoints.Count)
		{
			Debug.LogWarning($"Wave size was reduced from {waveSize} to {validSpawnPoints.Count} because each wave requires distinct spawn points.", this);
			waveSize = validSpawnPoints.Count;
		}

		validationError = null;
		return true;
	}

	private void NormalizeRuntimeValues()
	{
		numberOfWaves = Mathf.Max(1, numberOfWaves);
		maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
		waveSize = Mathf.Clamp(waveSize, 1, maxActiveEnemies);
		corpseLifetime = Mathf.Max(0f, corpseLifetime);
		NormalizeIntervalRange(ref minWaveInterval, ref maxWaveInterval);
		NormalizeIntervalRange(ref minTrickleInterval, ref maxTrickleInterval);
	}

	private static void NormalizeIntervalRange(ref float minimum, ref float maximum)
	{
		minimum = Mathf.Max(MinimumSpawnInterval, minimum);
		maximum = Mathf.Max(MinimumSpawnInterval, maximum);
		if (minimum > maximum)
		{
			float previousMinimum = minimum;
			minimum = maximum;
			maximum = previousMinimum;
		}
	}

	private int GetEffectiveWaveSize()
	{
		return Mathf.Clamp(waveSize, 1, Mathf.Min(maxActiveEnemies, validSpawnPoints.Count));
	}

	private void RefreshValidSpawnPoints()
	{
		validSpawnPoints.Clear();
		if (spawnPoints == null)
		{
			return;
		}

		for (int i = 0; i < spawnPoints.Count; i++)
		{
			Transform spawnPoint = spawnPoints[i];
			if (spawnPoint != null && !validSpawnPoints.Contains(spawnPoint))
			{
				validSpawnPoints.Add(spawnPoint);
			}
		}
	}

	private int CountAssignedSpawnPoints()
	{
		if (spawnPoints == null)
		{
			return 0;
		}

		int count = 0;
		for (int i = 0; i < spawnPoints.Count; i++)
		{
			Transform spawnPoint = spawnPoints[i];
			if (spawnPoint != null)
			{
				count++;
			}
		}

		return count;
	}
}
