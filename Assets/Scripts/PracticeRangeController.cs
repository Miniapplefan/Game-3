using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PracticeRangeController : MonoBehaviour
{
    public GameObject player;

    [Header("Target Settings")]
    [Tooltip("Prefab of the target to spawn")]
    public GameObject targetPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Minimum distance from the controller to spawn targets")]
    public float minDistance = 5f;

    [Tooltip("Maximum distance from the controller to spawn targets")]
    public float maxDistance = 20f;

    [Tooltip("Minimum height above the ground to spawn targets")]
    public float minHeight = 0f;

    [Tooltip("Maximum height above the ground to spawn targets")]
    public float maxHeight = 3f;

    [Tooltip("Minimum time between spawns (seconds)")]
    public float minSpawnInterval = 1f;

    [Tooltip("Maximum time between spawns (seconds)")]
    public float maxSpawnInterval = 3f;

    [Header("Session Settings")]
    [Tooltip("How long (in seconds) the range is active and spawning targets")]
    public float sessionDuration = 30f;
    public bool isByNumberOfTargets = false;
    public int numberOfTargets = 0;

    [HideInInspector]
    public int totalTargetsSpawned = 0;
    [HideInInspector]
    public int totalTargetsDestroyed = 0;
    [HideInInspector]
    public int totalTargetsAccountedFor = 0;

    private float sessionTimer = 0f;
    private bool sessionActive = false;
    private List<GameObject> spawnedTargets = new List<GameObject>();

    private Coroutine spawnRoutine;

    private bool displayedResults = false;

    void Start()
    {
        // Optional: start automatically
        StartRangeSession();
    }

    private void Update()
    {
        if (totalTargetsAccountedFor == totalTargetsSpawned && sessionActive == false && displayedResults == false)
        {
            displayedResults = true;
            Debug.Log($"Session ended. Total targets hit: {totalTargetsDestroyed} / {totalTargetsSpawned}");
        }
    }

    public void StartRangeSession()
    {
        if (targetPrefab == null)
        {
            Debug.LogWarning("No target prefab assigned to PracticeRangeController.");
            return;
        }

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);

        spawnedTargets.Clear();
        totalTargetsSpawned = 0;
        totalTargetsDestroyed = 0;
        totalTargetsAccountedFor = 0;
        sessionTimer = 0f;
        sessionActive = true;
        displayedResults = false;

        spawnRoutine = StartCoroutine(SpawnTargets());
    }

    public void StopRangeSession()
    {
        sessionActive = false;

        if (spawnRoutine != null)
            StopCoroutine(spawnRoutine);
    }

    private IEnumerator SpawnTargets()
    {
        if (isByNumberOfTargets)
        {
            // --- Number-based mode ---
            while (totalTargetsSpawned < numberOfTargets)
            {
                float interval = Random.Range(minSpawnInterval, maxSpawnInterval);
                yield return new WaitForSeconds(interval);

                SpawnTarget();
            }

            // When the desired number of targets has been spawned, stop.
            StopRangeSession();
        }
        else
        {
            // --- Time-based mode (original behavior) ---
            while (sessionTimer < sessionDuration)
            {
                float interval = Random.Range(minSpawnInterval, maxSpawnInterval);
                yield return new WaitForSeconds(interval);

                SpawnTarget();
                sessionTimer += interval;
            }

            StopRangeSession();
        }
    }

    private void SpawnTarget()
    {
        float randomAngle = Random.Range(-90f, 90f); // 180° arc in front
        float randomDistance = Random.Range(minDistance, maxDistance);
        float randomHeight = Random.Range(minHeight, maxHeight);

        // Direction in local space, then convert to world
        Vector3 direction = Quaternion.Euler(0f, randomAngle, 0f) * transform.forward;
        Vector3 spawnPosition = transform.position + direction * randomDistance;
        spawnPosition.y += randomHeight;

        GameObject target = Instantiate(targetPrefab, spawnPosition, Quaternion.identity);
        target.GetComponentInChildren<PracticeTarget>().prc = this;
        target.GetComponentInChildren<PracticeTarget>().player = player;

        spawnedTargets.Add(target);

        totalTargetsSpawned++;
    }

    public int GetTotalTargetsSpawned()
    {
        return totalTargetsSpawned;
    }

    public List<GameObject> GetSpawnedTargets()
    {
        return spawnedTargets;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Visualize the spawn area in the editor
        Gizmos.color = Color.yellow;
        Vector3 leftDir = Quaternion.Euler(0f, -90f, 0f) * transform.forward;
        Vector3 rightDir = Quaternion.Euler(0f, 90f, 0f) * transform.forward;

        Gizmos.DrawRay(transform.position, leftDir * maxDistance);
        Gizmos.DrawRay(transform.position, rightDir * maxDistance);
        Gizmos.DrawWireSphere(transform.position, minDistance);
        Gizmos.DrawWireSphere(transform.position, maxDistance);
    }
#endif
}
