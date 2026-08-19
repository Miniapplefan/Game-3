using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    private enum BulletTelegraphState
    {
        Neutral,
        Graze,
        Lethal
    }

    public float speed;
    public MeshRenderer bulletTipMesh;
    public MeshRenderer bulletBodyMesh;
    public TrailRenderer trail;
    public Material hitTelegraphMaterial;
    public Material noHitTelegraphMaterial;

    [SerializeField] LayerMask hitMask;
    [SerializeField] LayerMask telegraphMask;
    [SerializeField] float collisionRadius = 0.1f;
    [SerializeField] float telegraphRadiusMultiplier = 1.1f;
    [SerializeField] int marchingCubesDamage = 1;
    [SerializeField] float telegraphMaxDistance = 0f;
    [SerializeField] private BulletTimeChannel bulletTimeChannel = BulletTimeChannel.EnemyBullet;

    [Header("Player Graze")]
    [SerializeField] private bool enablePlayerGraze = true;
    [SerializeField, Min(0f)] private float grazeDistance = 0.35f;
    [SerializeField] private LayerMask grazeMask;
    [SerializeField, ColorUsage(false, false)] private Color grazeTint = new Color(0.9f, 0.96f, 1f, 1f);

    bool hasPlayerCandidate = false;

    PlayerController playerCandidate;
    readonly HashSet<Collider> playerOverlaps = new HashSet<Collider>();
    CapsuleCollider telegraphCollider;

    [SerializeField] private float lifetime = 10f; // Seconds before the bullet is destroyed
    private float lifeTimer;
    private Action<Bullet> releaseAction;
    private bool released;
    private bool trailEnabled = true;
    private AudioLoopHandle lethalWarningLoop;
    private BulletTelegraphState telegraphState = BulletTelegraphState.Neutral;
    private MaterialPropertyBlock grazeTintProperties;

    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");

    private const int GrazeOverlapCapacity = 32;
    private readonly Collider[] grazeOverlapResults = new Collider[GrazeOverlapCapacity];
    private BodyController pendingGrazePlayer;
    private Collider[] pendingGrazePlayerColliders;
    private Vector3 pendingGrazeTravelDirection;
    private bool grazeResolved;

    void Awake()
    {
        telegraphCollider = GetComponent<CapsuleCollider>();
        grazeTintProperties = new MaterialPropertyBlock();
        if (telegraphMaxDistance <= 0f)
        {
            telegraphMaxDistance = ComputeTelegraphDistance();
        }
        if (telegraphMask == 0)
        {
            telegraphMask = hitMask;
        }
        if (grazeMask == 0)
        {
            grazeMask = telegraphMask;
        }

        ConfigureTelegraphCollider();
        ApplyTelegraphVisuals(BulletTelegraphState.Neutral);
    }

    void OnValidate()
    {
        grazeDistance = Mathf.Max(0f, grazeDistance);
        telegraphCollider = GetComponent<CapsuleCollider>();
        ConfigureTelegraphCollider();
    }

    void OnEnable()
    {
        ResetState();
        lifeTimer = lifetime;
        released = false;
    }

    void OnDisable()
    {
        ReleaseLethalWarningLoop();
        playerOverlaps.Clear();
        playerCandidate = null;
        hasPlayerCandidate = false;
        telegraphState = BulletTelegraphState.Neutral;
    }

    // Update is called once per frame
    void Update()
    {
        float deltaTime = BulletTimeManager.GetDeltaTime(bulletTimeChannel);
        lifeTimer -= deltaTime;
        if (lifeTimer <= 0f)
        {
            Release();
            return;
        }

        Vector3 startPosition = transform.position;
        Vector3 step = transform.forward * speed * deltaTime;
        Vector3 endPosition = startPosition + step;

        if (HandleCollision(startPosition, endPosition))
        {
            return;
        }

        transform.position = endPosition;
        UpdatePlayerGraze(startPosition, endPosition);

        BulletTelegraphState targetTelegraphState = EvaluateTelegraphState();
        UpdateTelegraphState(targetTelegraphState);
    }

    private bool HandleCollision(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 delta = endPosition - startPosition;
        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 travelDirection = delta.normalized;
        if (Physics.SphereCast(startPosition, collisionRadius, travelDirection, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            ProcessHit(hit, -travelDirection);
            Release();
            return true;
        }

        return false;
    }

    private void UpdatePlayerGraze(Vector3 startPosition, Vector3 endPosition)
    {
        if (!enablePlayerGraze || grazeResolved)
        {
            return;
        }

        Vector3 travelDelta = endPosition - startPosition;
        if (travelDelta.sqrMagnitude <= Mathf.Epsilon)
        {
            return;
        }

        if (pendingGrazePlayer == null)
        {
            pendingGrazePlayer = FindGrazePlayer(startPosition, endPosition);
            if (pendingGrazePlayer == null)
            {
                return;
            }

            pendingGrazeTravelDirection = travelDelta.normalized;
            pendingGrazePlayerColliders = pendingGrazePlayer.GetComponentsInChildren<Collider>(true);
        }

        if (!IsEligibleGrazePlayer(pendingGrazePlayer))
        {
            ClearPendingGraze();
            return;
        }

        if (!HasPassedGrazePlayer(endPosition))
        {
            return;
        }

        grazeResolved = true;
        AuraManager auraManager = pendingGrazePlayer.auraManager;
        if (auraManager == null)
        {
            auraManager = pendingGrazePlayer.GetComponent<AuraManager>();
            pendingGrazePlayer.auraManager = auraManager;
        }

        if (auraManager != null)
        {
            auraManager.TryRegisterGraze();
        }

        ClearPendingGraze();
    }

    private BodyController FindGrazePlayer(Vector3 startPosition, Vector3 endPosition)
    {
        float outerRadius = Mathf.Max(0f, collisionRadius) + Mathf.Max(0f, grazeDistance);
        int overlapCount = Physics.OverlapCapsuleNonAlloc(
            startPosition,
            endPosition,
            outerRadius,
            grazeOverlapResults,
            grazeMask,
            QueryTriggerInteraction.Ignore
        );

        for (int i = 0; i < overlapCount; i++)
        {
            Collider overlap = grazeOverlapResults[i];
            grazeOverlapResults[i] = null;
            if (overlap == null || overlap.GetComponentInParent<PlayerController>() == null)
            {
                continue;
            }

            BodyController bodyController = overlap.GetComponentInParent<BodyController>();
            if (IsEligibleGrazePlayer(bodyController))
            {
                ClearGrazeOverlapResults(i + 1, overlapCount);
                return bodyController;
            }
        }

        return null;
    }

    private bool HasPassedGrazePlayer(Vector3 bulletPosition)
    {
        if (pendingGrazePlayerColliders == null || pendingGrazeTravelDirection.sqrMagnitude <= Mathf.Epsilon)
        {
            return false;
        }

        bool foundPlayerCollider = false;
        float forwardmostProjection = float.NegativeInfinity;
        Vector3 absoluteDirection = new Vector3(
            Mathf.Abs(pendingGrazeTravelDirection.x),
            Mathf.Abs(pendingGrazeTravelDirection.y),
            Mathf.Abs(pendingGrazeTravelDirection.z)
        );

        foreach (Collider playerCollider in pendingGrazePlayerColliders)
        {
            if (playerCollider == null
                || !playerCollider.enabled
                || !playerCollider.gameObject.activeInHierarchy
                || playerCollider.isTrigger
                || !LayerIsInMask(playerCollider.gameObject.layer, grazeMask))
            {
                continue;
            }

            Bounds bounds = playerCollider.bounds;
            float colliderProjection = Vector3.Dot(bounds.center, pendingGrazeTravelDirection)
                + Vector3.Dot(bounds.extents, absoluteDirection);
            forwardmostProjection = Mathf.Max(forwardmostProjection, colliderProjection);
            foundPlayerCollider = true;
        }

        if (!foundPlayerCollider)
        {
            return false;
        }

        float outerRadius = Mathf.Max(0f, collisionRadius) + Mathf.Max(0f, grazeDistance);
        float bulletProjection = Vector3.Dot(bulletPosition, pendingGrazeTravelDirection);
        return bulletProjection > forwardmostProjection + outerRadius;
    }

    private static bool IsEligibleGrazePlayer(BodyController bodyController)
    {
        return bodyController != null && !bodyController.isAI && !bodyController.isDead;
    }

    private static bool LayerIsInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private void ClearGrazeOverlapResults(int startIndex, int endIndex)
    {
        for (int i = startIndex; i < endIndex; i++)
        {
            grazeOverlapResults[i] = null;
        }
    }

    private void ClearPendingGraze()
    {
        pendingGrazePlayer = null;
        pendingGrazePlayerColliders = null;
        pendingGrazeTravelDirection = Vector3.zero;
    }

    private void ProcessHit(RaycastHit hit, Vector3 incomingDirection)
    {
        PlayerController player = hit.collider.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            BodyController bodyController = hit.collider.GetComponentInParent<BodyController>();
            if (bodyController != null && !bodyController.isGodMode)
            {
                bodyController.DieFacingIncomingDirection(incomingDirection);
            }
            //Debug.Log(hit.collider.name + " " + Time.timeSinceLevelLoadAsDouble);
            return;
        }

        MarchingCubesGenerator marchingCubes = hit.collider.GetComponentInParent<MarchingCubesGenerator>();
        if (marchingCubes != null)
        {
            marchingCubes.TakeDamage(hit.point, marchingCubesDamage);
        }
    }

    public void SetPoolRelease(Action<Bullet> release)
    {
        releaseAction = release;
    }

    private void Release()
    {
        if (released)
        {
            return;
        }

        released = true;
        ReleaseLethalWarningLoop();
        if (releaseAction != null)
        {
            releaseAction(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private bool HasLineOfSightToPlayer()
    {
        if (playerCandidate == null)
        {
            return false;
        }

        float maxDistance = telegraphMaxDistance > 0f ? telegraphMaxDistance : Mathf.Infinity;
        float telegraphRadius = Mathf.Max(0f, collisionRadius * telegraphRadiusMultiplier);
        if (Physics.SphereCast(transform.position, telegraphRadius, transform.forward, out RaycastHit hit, maxDistance, telegraphMask, QueryTriggerInteraction.Ignore))
        {
            return hit.collider.GetComponentInParent<PlayerController>() != null;
        }

        return false;
    }

    private bool HasGrazeTrajectoryToPlayer()
    {
        if (!enablePlayerGraze || playerCandidate == null)
        {
            return false;
        }

        float maxDistance = telegraphMaxDistance > 0f ? telegraphMaxDistance : Mathf.Infinity;
        float outerRadius = Mathf.Max(0f, collisionRadius) + Mathf.Max(0f, grazeDistance);
        if (!Physics.SphereCast(
            transform.position,
            outerRadius,
            transform.forward,
            out RaycastHit hit,
            maxDistance,
            grazeMask,
            QueryTriggerInteraction.Ignore
        ))
        {
            return false;
        }

        PlayerController hitPlayer = hit.collider.GetComponentInParent<PlayerController>();
        if (hitPlayer == null || hitPlayer != playerCandidate)
        {
            return false;
        }

        return IsEligibleGrazePlayer(hit.collider.GetComponentInParent<BodyController>());
    }

    private BulletTelegraphState EvaluateTelegraphState()
    {
        if (!hasPlayerCandidate)
        {
            return BulletTelegraphState.Neutral;
        }

        if (HasLineOfSightToPlayer())
        {
            return BulletTelegraphState.Lethal;
        }

        return HasGrazeTrajectoryToPlayer()
            ? BulletTelegraphState.Graze
            : BulletTelegraphState.Neutral;
    }

    private void UpdateTelegraphState(BulletTelegraphState targetState, bool forceVisualRefresh = false)
    {
        bool stateChanged = targetState != telegraphState;
        if (stateChanged || forceVisualRefresh)
        {
            ApplyTelegraphVisuals(targetState);
        }

        bool isLethal = targetState == BulletTelegraphState.Lethal;
        if (isLethal)
        {
            if (lethalWarningLoop == null || !lethalWarningLoop.IsValid)
            {
                lethalWarningLoop = AudioService.PlayFollowingLoop(
                    GameAudioCueId.EnemyLethalBulletWarning,
                    transform
                );
            }
            else if (stateChanged)
            {
                lethalWarningLoop.SetActive(true);
            }
        }
        else if (stateChanged && lethalWarningLoop != null && lethalWarningLoop.IsValid)
        {
            lethalWarningLoop.SetActive(false);
        }

        telegraphState = targetState;
    }

    private void ApplyTelegraphVisuals(BulletTelegraphState targetState)
    {
        Material targetMaterial = targetState == BulletTelegraphState.Lethal
            ? hitTelegraphMaterial
            : noHitTelegraphMaterial;
        MaterialPropertyBlock properties = null;

        if (targetState == BulletTelegraphState.Graze)
        {
            if (grazeTintProperties == null)
            {
                grazeTintProperties = new MaterialPropertyBlock();
            }

            grazeTintProperties.Clear();
            grazeTintProperties.SetColor(ColorPropertyId, grazeTint);
            properties = grazeTintProperties;
        }

        ApplyTelegraphVisual(bulletTipMesh, targetMaterial, properties);
        ApplyTelegraphVisual(bulletBodyMesh, targetMaterial, properties);
        ApplyTelegraphVisual(trail, targetMaterial, properties);
    }

    private static void ApplyTelegraphVisual(Renderer renderer, Material material, MaterialPropertyBlock properties)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sharedMaterial = material;
        renderer.SetPropertyBlock(properties);
    }

    private void ReleaseLethalWarningLoop()
    {
        if (lethalWarningLoop == null)
        {
            return;
        }

        lethalWarningLoop.Release();
        lethalWarningLoop = null;
    }

    private float ComputeTelegraphDistance()
    {
        if (telegraphCollider == null)
        {
            return 0f;
        }

        float axisScale = 1f;
        float axisCenter = 0f;
        switch (telegraphCollider.direction)
        {
            case 0:
                axisScale = transform.lossyScale.x;
                axisCenter = telegraphCollider.center.x;
                break;
            case 1:
                axisScale = transform.lossyScale.y;
                axisCenter = telegraphCollider.center.y;
                break;
            case 2:
                axisScale = transform.lossyScale.z;
                axisCenter = telegraphCollider.center.z;
                break;
        }

        float axisExtent = telegraphCollider.height * 0.5f;
        float forwardDistance = (axisCenter + axisExtent) * axisScale;
        return Mathf.Max(0f, forwardDistance);
    }

    private void ConfigureTelegraphCollider()
    {
        if (telegraphCollider == null)
        {
            return;
        }

        float lethalCandidateRadius = Mathf.Max(0f, collisionRadius * telegraphRadiusMultiplier);
        float grazeCandidateRadius = Mathf.Max(0f, collisionRadius) + Mathf.Max(0f, grazeDistance);
        telegraphCollider.radius = enablePlayerGraze
            ? Mathf.Max(lethalCandidateRadius, grazeCandidateRadius)
            : lethalCandidateRadius;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        playerOverlaps.Add(other);
        playerCandidate = player;
        hasPlayerCandidate = playerOverlaps.Count > 0;
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null)
        {
            return;
        }

        playerOverlaps.Remove(other);
        if (playerOverlaps.Count == 0)
        {
            hasPlayerCandidate = false;
            playerCandidate = null;
        }
    }

    private void ResetState()
    {
        ReleaseLethalWarningLoop();
        hasPlayerCandidate = false;
        playerCandidate = null;
        playerOverlaps.Clear();
        grazeResolved = false;
        ClearPendingGraze();
        ClearGrazeOverlapResults(0, grazeOverlapResults.Length);

        UpdateTelegraphState(BulletTelegraphState.Neutral, true);
        if (trail != null)
        {
            trail.enabled = trailEnabled;
            trail.emitting = trailEnabled;
            trail.Clear();
        }
    }

    public void SetTrailEnabled(bool enabled)
    {
        trailEnabled = enabled;
        if (trail != null)
        {
            trail.enabled = enabled;
            trail.emitting = enabled;
            if (!enabled)
            {
                trail.Clear();
            }
        }
    }
}
