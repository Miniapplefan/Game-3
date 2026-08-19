using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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
	[SerializeField, Min(0f), Tooltip("Seconds a dead enemy remains as a ragdoll before being returned to the pool.")]
	private float corpseLifetime = 5f;

	[Header("Pooling")]
	[SerializeField, Min(0), Tooltip("Enemies created up front so later waves do not need to instantiate them. Set this near the expected peak of living enemies plus visible corpses.")]
	private int prewarmPoolSize = 10;

	[SerializeField, Tooltip("Create additional pooled enemies if every prewarmed enemy is in use.")]
	private bool allowPoolExpansion = true;

	[SerializeField, Min(0.1f), Tooltip("Maximum distance used to snap an enemy spawn point onto the NavMesh.")]
	private float navMeshSpawnSampleRadius = 2f;

	[Header("Lifecycle")]
	[SerializeField, Tooltip("Begin spawning automatically when this component becomes enabled.")]
	private bool autoStart = true;

	private readonly List<TrackedEnemy> trackedEnemies = new List<TrackedEnemy>();
	private readonly List<Transform> validSpawnPoints = new List<Transform>();
	private readonly List<Transform> waveCandidates = new List<Transform>();
	private readonly Queue<PooledEnemy> availableEnemies = new Queue<PooledEnemy>();
	private readonly List<PooledEnemy> pooledEnemies = new List<PooledEnemy>();
	private readonly HashSet<Transform> warnedOffMeshSpawnPoints = new HashSet<Transform>();
	private Coroutine spawnCoroutine;
	private Transform poolRoot;
	private bool hasSpawnedOpeningWave;

	public int ActiveEnemyCount { get; private set; }
	public int TrackedCorpseCount { get; private set; }
	public int WavesSpawned { get; private set; }
	public int AvailablePoolCount => availableEnemies.Count;
	public int TotalPoolCount => pooledEnemies.Count;
	public bool IsSpawning => spawnCoroutine != null;
	public bool HasReachedWaveLimit => limitWaveCount && WavesSpawned >= numberOfWaves;

	private enum EnemyState
	{
		Alive,
		Corpse
	}

	private sealed class TrackedEnemy
	{
		public PooledEnemy Enemy;
		public EnemyState State;
		public float CleanupTime;
	}

	private sealed class PooledEnemy
	{
		public GameObject Root;
		public BodyController Body;
		public TransformState[] Transforms;
		public RigidbodyState[] Rigidbodies;
		public JointState[] Joints;
		public ActiveRagdollController ActiveRagdoll;
		public NavMeshAgent NavMeshAgent;
		public NPCBrain Brain;
		public List<IEnemyPoolResettable> Resetters;
		public bool ActiveRagdollInitiallyEnabled;
		public bool HasBeenSpawned;
		public bool IsAvailable;
	}

	private struct TransformState
	{
		public Transform Transform;
		public Vector3 LocalPosition;
		public Quaternion LocalRotation;
		public Vector3 LocalScale;
		public bool ActiveSelf;
	}

	private struct RigidbodyState
	{
		public Rigidbody Rigidbody;
		public float Drag;
		public float AngularDrag;
		public bool IsKinematic;
		public bool UseGravity;
		public RigidbodyConstraints Constraints;
		public CollisionDetectionMode CollisionDetectionMode;
		public RigidbodyInterpolation Interpolation;
	}

	private struct JointState
	{
		public ConfigurableJoint Joint;
		public JointDrive AngularXDrive;
		public JointDrive AngularYZDrive;
		public JointDrive SlerpDrive;
	}

	private void Awake()
	{
		EnsurePoolRoot();
		PrewarmPool();
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
		for (int i = pooledEnemies.Count - 1; i >= 0; i--)
		{
			PooledEnemy enemy = pooledEnemies[i];
			if (enemy != null && enemy.Root != null)
			{
				Destroy(enemy.Root);
			}
		}

		trackedEnemies.Clear();
		availableEnemies.Clear();
		pooledEnemies.Clear();
		ActiveEnemyCount = 0;
		TrackedCorpseCount = 0;
	}

	private void OnValidate()
	{
		numberOfWaves = Mathf.Max(1, numberOfWaves);
		maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
		waveSize = Mathf.Clamp(waveSize, 1, maxActiveEnemies);
		corpseLifetime = Mathf.Max(0f, corpseLifetime);
		prewarmPoolSize = Mathf.Max(0, prewarmPoolSize);
		navMeshSpawnSampleRadius = Mathf.Max(0.1f, navMeshSpawnSampleRadius);

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

		PooledEnemy enemy = TakeFromPool();
		if (enemy == null)
		{
			return false;
		}

		PrepareEnemyForSpawn(enemy, spawnPoint);

		trackedEnemies.Add(new TrackedEnemy
		{
			Enemy = enemy,
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
			PooledEnemy pooledEnemy = enemy.Enemy;

			if (pooledEnemy == null || pooledEnemy.Root == null)
			{
				RemoveTrackedEnemyAt(i, enemy.State);
				continue;
			}

			if (enemy.State == EnemyState.Alive)
			{
				if (pooledEnemy.Body == null)
				{
					ReturnToPool(pooledEnemy);
					RemoveTrackedEnemyAt(i, EnemyState.Alive);
					continue;
				}

				if (pooledEnemy.Body.isDead)
				{
					enemy.State = EnemyState.Corpse;
					enemy.CleanupTime = Time.time + corpseLifetime;
					ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
					TrackedCorpseCount++;
				}
			}

			if (enemy.State == EnemyState.Corpse && Time.time >= enemy.CleanupTime)
			{
				ReturnToPool(pooledEnemy);
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

	private void EnsurePoolRoot()
	{
		if (poolRoot != null)
		{
			return;
		}

		GameObject rootObject = new GameObject("Enemy Pool");
		poolRoot = rootObject.transform;
		poolRoot.SetParent(transform, false);
	}

	private void PrewarmPool()
	{
		if (enemyPrefab == null || enemyPrefab.GetComponentInChildren<BodyController>(true) == null)
		{
			return;
		}

		for (int i = pooledEnemies.Count; i < prewarmPoolSize; i++)
		{
			if (CreatePooledEnemy() == null)
			{
				break;
			}
		}
	}

	private PooledEnemy TakeFromPool()
	{
		while (availableEnemies.Count > 0)
		{
			PooledEnemy enemy = availableEnemies.Dequeue();
			if (enemy != null && enemy.Root != null)
			{
				enemy.IsAvailable = false;
				return enemy;
			}
		}

		if (!allowPoolExpansion)
		{
			return null;
		}

		PooledEnemy expandedEnemy = CreatePooledEnemy();
		if (expandedEnemy == null)
		{
			return null;
		}

		// Newly created entries are queued by CreatePooledEnemy.
		return TakeFromPool();
	}

	private PooledEnemy CreatePooledEnemy()
	{
		EnsurePoolRoot();
		GameObject enemyRoot = Instantiate(enemyPrefab, poolRoot);
		BodyController body = enemyRoot.GetComponentInChildren<BodyController>(true);
		if (body == null)
		{
			Debug.LogError($"Enemy prefab '{enemyPrefab.name}' has no {nameof(BodyController)} in its hierarchy.", enemyPrefab);
			Destroy(enemyRoot);
			return null;
		}

		PooledEnemy enemy = new PooledEnemy
		{
			Root = enemyRoot,
			Body = body,
			Transforms = CaptureTransforms(enemyRoot),
			Rigidbodies = CaptureRigidbodies(enemyRoot),
			Joints = CaptureJoints(enemyRoot),
			ActiveRagdoll = enemyRoot.GetComponentInChildren<ActiveRagdollController>(true),
			NavMeshAgent = enemyRoot.GetComponentInChildren<NavMeshAgent>(true),
			Brain = enemyRoot.GetComponentInChildren<NPCBrain>(true),
			Resetters = FindPoolResettables(enemyRoot),
			IsAvailable = true
		};
		enemy.ActiveRagdollInitiallyEnabled = enemy.ActiveRagdoll != null && enemy.ActiveRagdoll.enabled;

		enemyRoot.SetActive(false);
		pooledEnemies.Add(enemy);
		availableEnemies.Enqueue(enemy);
		return enemy;
	}

	private void PrepareEnemyForSpawn(PooledEnemy enemy, Transform spawnPoint)
	{
		enemy.Root.SetActive(false);
		enemy.Root.transform.SetParent(null, true);
		RestoreCachedState(enemy);
		bool isReusedEnemy = enemy.HasBeenSpawned;

		if (isReusedEnemy)
		{
			enemy.Body.PrepareForPoolActivation();
		}

		Vector3 spawnPosition = ResolveNavMeshSpawnPosition(enemy, spawnPoint);
		enemy.Root.transform.SetPositionAndRotation(spawnPosition, spawnPoint.rotation);
		enemy.Root.SetActive(true);

		if (isReusedEnemy)
		{
			ResetEnemyForPoolReuse(enemy);
		}

		enemy.HasBeenSpawned = true;
	}

	private Vector3 ResolveNavMeshSpawnPosition(PooledEnemy enemy, Transform spawnPoint)
	{
		if (enemy.NavMeshAgent == null)
		{
			return spawnPoint.position;
		}

		if (NavMesh.SamplePosition(
			spawnPoint.position,
			out NavMeshHit hit,
			navMeshSpawnSampleRadius,
			enemy.NavMeshAgent.areaMask))
		{
			warnedOffMeshSpawnPoints.Remove(spawnPoint);
			return hit.position;
		}

		if (warnedOffMeshSpawnPoints.Add(spawnPoint))
		{
			Debug.LogWarning(
				$"{nameof(EndlessWaveManager)} could not find a NavMesh position within {navMeshSpawnSampleRadius:0.##} units of spawn point '{spawnPoint.name}'.",
				spawnPoint);
		}

		return spawnPoint.position;
	}

	private static void ResetEnemyForPoolReuse(PooledEnemy enemy)
	{
		// Body models must exist before movement and decision components read them.
		enemy.Body.ResetForPoolReuse();

		if (enemy.NavMeshAgent != null)
		{
			enemy.NavMeshAgent.updatePosition = true;
			enemy.NavMeshAgent.updateRotation = true;
			if (enemy.NavMeshAgent.isOnNavMesh)
			{
				enemy.NavMeshAgent.isStopped = false;
				enemy.NavMeshAgent.ResetPath();
				enemy.NavMeshAgent.velocity = Vector3.zero;
			}
		}

		for (int i = 0; i < enemy.Resetters.Count; i++)
		{
			IEnemyPoolResettable resetter = enemy.Resetters[i];
			if (resetter == null || ReferenceEquals(resetter, enemy.Body) || ReferenceEquals(resetter, enemy.Brain))
			{
				continue;
			}

			resetter.ResetForPoolReuse();
		}

		// Replanning happens last so all state and NavMesh settings are ready first.
		if (enemy.Brain != null)
		{
			enemy.Brain.ResetForPoolReuse();
		}

		if (enemy.NavMeshAgent != null && enemy.NavMeshAgent.enabled && !enemy.NavMeshAgent.isOnNavMesh)
		{
			Debug.LogWarning($"Pooled enemy '{enemy.Root.name}' was reset but could not bind to the NavMesh.", enemy.Root);
		}
	}

	private static List<IEnemyPoolResettable> FindPoolResettables(GameObject root)
	{
		MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
		List<IEnemyPoolResettable> resetters = new List<IEnemyPoolResettable>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] is IEnemyPoolResettable resetter)
			{
				resetters.Add(resetter);
			}
		}

		return resetters;
	}

	private void ReturnToPool(PooledEnemy enemy)
	{
		if (enemy == null || enemy.Root == null || enemy.IsAvailable)
		{
			return;
		}

		enemy.Root.SetActive(false);
		enemy.Root.transform.SetParent(poolRoot, false);
		enemy.Resetters = FindPoolResettables(enemy.Root);
		enemy.IsAvailable = true;
		availableEnemies.Enqueue(enemy);
	}

	private static TransformState[] CaptureTransforms(GameObject root)
	{
		Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
		TransformState[] states = new TransformState[transforms.Length];
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform cachedTransform = transforms[i];
			states[i] = new TransformState
			{
				Transform = cachedTransform,
				LocalPosition = cachedTransform.localPosition,
				LocalRotation = cachedTransform.localRotation,
				LocalScale = cachedTransform.localScale,
				ActiveSelf = cachedTransform.gameObject.activeSelf
			};
		}

		return states;
	}

	private static RigidbodyState[] CaptureRigidbodies(GameObject root)
	{
		Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
		RigidbodyState[] states = new RigidbodyState[rigidbodies.Length];
		for (int i = 0; i < rigidbodies.Length; i++)
		{
			Rigidbody body = rigidbodies[i];
			states[i] = new RigidbodyState
			{
				Rigidbody = body,
				Drag = body.drag,
				AngularDrag = body.angularDrag,
				IsKinematic = body.isKinematic,
				UseGravity = body.useGravity,
				Constraints = body.constraints,
				CollisionDetectionMode = body.collisionDetectionMode,
				Interpolation = body.interpolation
			};
		}

		return states;
	}

	private static JointState[] CaptureJoints(GameObject root)
	{
		ConfigurableJoint[] joints = root.GetComponentsInChildren<ConfigurableJoint>(true);
		JointState[] states = new JointState[joints.Length];
		for (int i = 0; i < joints.Length; i++)
		{
			ConfigurableJoint joint = joints[i];
			states[i] = new JointState
			{
				Joint = joint,
				AngularXDrive = joint.angularXDrive,
				AngularYZDrive = joint.angularYZDrive,
				SlerpDrive = joint.slerpDrive
			};
		}

		return states;
	}

	private static void RestoreCachedState(PooledEnemy enemy)
	{
		for (int i = 0; i < enemy.Transforms.Length; i++)
		{
			TransformState state = enemy.Transforms[i];
			if (state.Transform == null || state.Transform == enemy.Root.transform)
			{
				continue;
			}

			state.Transform.localPosition = state.LocalPosition;
			state.Transform.localRotation = state.LocalRotation;
			state.Transform.localScale = state.LocalScale;
			state.Transform.gameObject.SetActive(state.ActiveSelf);
		}

		for (int i = 0; i < enemy.Joints.Length; i++)
		{
			JointState state = enemy.Joints[i];
			if (state.Joint == null)
			{
				continue;
			}

			state.Joint.angularXDrive = state.AngularXDrive;
			state.Joint.angularYZDrive = state.AngularYZDrive;
			state.Joint.slerpDrive = state.SlerpDrive;
		}

		for (int i = 0; i < enemy.Rigidbodies.Length; i++)
		{
			RigidbodyState state = enemy.Rigidbodies[i];
			Rigidbody body = state.Rigidbody;
			if (body == null)
			{
				continue;
			}

			body.velocity = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.drag = state.Drag;
			body.angularDrag = state.AngularDrag;
			body.isKinematic = state.IsKinematic;
			body.useGravity = state.UseGravity;
			body.constraints = state.Constraints;
			body.collisionDetectionMode = state.CollisionDetectionMode;
			body.interpolation = state.Interpolation;
			body.Sleep();
		}

		if (enemy.ActiveRagdoll != null)
		{
			enemy.ActiveRagdoll.enabled = enemy.ActiveRagdollInitiallyEnabled;
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
		prewarmPoolSize = Mathf.Max(0, prewarmPoolSize);
		navMeshSpawnSampleRadius = Mathf.Max(0.1f, navMeshSpawnSampleRadius);
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
