using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;

[RequireComponent(typeof(NavMeshSurface))]
public class RuntimeNavMeshUpdater : MonoBehaviour
{
	private static RuntimeNavMeshUpdater Instance;

	[SerializeField] private float rebuildDelay = 0.1f;
	[SerializeField] private float minimumRebuildInterval = 0.35f;
	[SerializeField] private bool buildOnStart = true;
	[SerializeField] private bool logRebuilds = false;

	private NavMeshSurface navMeshSurface;
	private AsyncOperation activeUpdateOperation;
	private Coroutine rebuildCoroutine;
	private Bounds dirtyBounds;
	private bool hasDirtyBounds;
	private bool dirtyWhileUpdating;
	private int dirtyRequestCount;
	private float lastRebuildTime = float.NegativeInfinity;

	public static void MarkDirty()
	{
		RuntimeNavMeshUpdater updater = ResolveInstance();
		if (updater == null)
		{
			return;
		}

		updater.MarkDirtyInternal();
	}

	public static void MarkDirty(Bounds changedBounds)
	{
		RuntimeNavMeshUpdater updater = ResolveInstance();
		if (updater == null)
		{
			return;
		}

		updater.MarkDirtyInternal(changedBounds);
	}

	private static RuntimeNavMeshUpdater ResolveInstance()
	{
		if (Instance == null)
		{
			Instance = FindObjectOfType<RuntimeNavMeshUpdater>();
		}

		return Instance;
	}

	private void Awake()
	{
		navMeshSurface = GetComponent<NavMeshSurface>();
		if (Instance == null || Instance == this)
		{
			Instance = this;
		}
		else if (logRebuilds)
		{
			Debug.LogWarning($"RuntimeNavMeshUpdater: duplicate updater '{name}' found. Static dirty requests will use '{Instance.name}'.");
		}
	}

	private IEnumerator Start()
	{
		if (!buildOnStart)
		{
			yield break;
		}

		yield return null;
		MarkDirtyInternal();
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	private void MarkDirtyInternal()
	{
		dirtyRequestCount++;
		EnsureRebuildCoroutine();
	}

	private void MarkDirtyInternal(Bounds changedBounds)
	{
		if (hasDirtyBounds)
		{
			dirtyBounds.Encapsulate(changedBounds);
		}
		else
		{
			dirtyBounds = changedBounds;
			hasDirtyBounds = true;
		}

		MarkDirtyInternal();
	}

	private void EnsureRebuildCoroutine()
	{
		if (activeUpdateOperation != null && !activeUpdateOperation.isDone)
		{
			dirtyWhileUpdating = true;
			return;
		}

		if (rebuildCoroutine == null)
		{
			rebuildCoroutine = StartCoroutine(RebuildWhenReady());
		}
	}

	private IEnumerator RebuildWhenReady()
	{
		yield return new WaitForSeconds(Mathf.Max(0f, rebuildDelay));

		float waitForInterval = minimumRebuildInterval - (Time.time - lastRebuildTime);
		if (waitForInterval > 0f)
		{
			yield return new WaitForSeconds(waitForInterval);
		}

		rebuildCoroutine = null;
		StartRebuild();
	}

	private void StartRebuild()
	{
		if (navMeshSurface == null)
		{
			navMeshSurface = GetComponent<NavMeshSurface>();
		}

		if (navMeshSurface == null)
		{
			return;
		}

		lastRebuildTime = Time.time;
		if (logRebuilds)
		{
			string boundsText = hasDirtyBounds
				? $" bounds center={dirtyBounds.center} size={dirtyBounds.size}"
				: string.Empty;
			Debug.Log($"RuntimeNavMeshUpdater: rebuilding NavMesh after {dirtyRequestCount} dirty request(s).{boundsText}");
		}

		dirtyRequestCount = 0;
		hasDirtyBounds = false;
		dirtyWhileUpdating = false;

		if (navMeshSurface.navMeshData == null)
		{
			navMeshSurface.BuildNavMesh();
			QueueFollowUpIfNeeded();
			return;
		}

		activeUpdateOperation = navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
		StartCoroutine(WaitForUpdateOperation());
	}

	private IEnumerator WaitForUpdateOperation()
	{
		while (activeUpdateOperation != null && !activeUpdateOperation.isDone)
		{
			yield return null;
		}

		activeUpdateOperation = null;
		QueueFollowUpIfNeeded();
	}

	private void QueueFollowUpIfNeeded()
	{
		if (dirtyWhileUpdating || dirtyRequestCount > 0)
		{
			dirtyWhileUpdating = false;
			EnsureRebuildCoroutine();
		}
	}
}
