using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
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

    bool shouldTelegraph = false;
    bool isTelegraph = false;
    bool hasPlayerCandidate = false;

    PlayerController playerCandidate;
    readonly HashSet<Collider> playerOverlaps = new HashSet<Collider>();
    CapsuleCollider telegraphCollider;

    [SerializeField] private float lifetime = 10f; // Seconds before the bullet is destroyed
    private float lifeTimer;
    private Action<Bullet> releaseAction;
    private bool released;
    private bool trailEnabled = true;

    void Awake()
    {
        telegraphCollider = GetComponent<CapsuleCollider>();
        if (telegraphMaxDistance <= 0f)
        {
            telegraphMaxDistance = ComputeTelegraphDistance();
        }
        if (telegraphMask == 0)
        {
            telegraphMask = hitMask;
        }

        bulletTipMesh.material = noHitTelegraphMaterial;
        bulletBodyMesh.material = noHitTelegraphMaterial;
        if (trail != null) trail.material = noHitTelegraphMaterial;
    }

    void OnEnable()
    {
        ResetState();
        lifeTimer = lifetime;
        released = false;
    }

    void OnDisable()
    {
        playerOverlaps.Clear();
        playerCandidate = null;
        hasPlayerCandidate = false;
        shouldTelegraph = false;
        isTelegraph = false;
    }

    // Update is called once per frame
    void Update()
    {
        lifeTimer -= Time.deltaTime;
        if (lifeTimer <= 0f)
        {
            Release();
            return;
        }

        Vector3 startPosition = transform.position;
        Vector3 step = transform.forward * speed * Time.deltaTime;
        Vector3 endPosition = startPosition + step;

        if (HandleCollision(startPosition, endPosition))
        {
            return;
        }

        transform.position = endPosition;

        shouldTelegraph = hasPlayerCandidate && HasLineOfSightToPlayer();
        UpdateTelegraphVisuals();
    }

    private bool HandleCollision(Vector3 startPosition, Vector3 endPosition)
    {
        Vector3 delta = endPosition - startPosition;
        float distance = delta.magnitude;
        if (distance <= Mathf.Epsilon)
        {
            return false;
        }

        if (Physics.SphereCast(startPosition, collisionRadius, delta.normalized, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
        {
            ProcessHit(hit);
            Release();
            return true;
        }

        return false;
    }

    private void ProcessHit(RaycastHit hit)
    {
        PlayerController player = hit.collider.GetComponentInParent<PlayerController>();
        if (player != null)
        {
            BodyController bodyController = hit.collider.GetComponentInParent<BodyController>();
            if (bodyController != null && !bodyController.isGodMode)
            {
                bodyController.Die();
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

    private void UpdateTelegraphVisuals()
    {
        if (shouldTelegraph == isTelegraph)
        {
            return;
        }

        Material targetMaterial = shouldTelegraph ? hitTelegraphMaterial : noHitTelegraphMaterial;
        if (bulletTipMesh != null) bulletTipMesh.material = targetMaterial;
        if (bulletBodyMesh != null) bulletBodyMesh.material = targetMaterial;
        if (trail != null) trail.material = targetMaterial;
        isTelegraph = shouldTelegraph;
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
        shouldTelegraph = false;
        isTelegraph = false;
        hasPlayerCandidate = false;
        playerCandidate = null;
        playerOverlaps.Clear();

        if (bulletTipMesh != null) bulletTipMesh.material = noHitTelegraphMaterial;
        if (bulletBodyMesh != null) bulletBodyMesh.material = noHitTelegraphMaterial;
        if (trail != null)
        {
            trail.material = noHitTelegraphMaterial;
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
