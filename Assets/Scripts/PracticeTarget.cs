using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PracticeTarget : MonoBehaviour, IDamageable, IHealthDisplaySource
{
    public enum TargetAxis
    {
        X,
        Y,
        Z
    }

    public event System.Action<PracticeTarget> DestroyedByPlayer;

    public float life;
    public float minLife;
    public float maxLife;

    [Header("Health Settings")]
    [Tooltip("The amount of damage this target can take before being destroyed.")]
    [Min(0.01f)]
    public float maxHealth = 1f;

    public float CurrentHealth => currentHealth;
    public float MaximumHealth => Mathf.Max(0.01f, maxHealth);
    public bool IsDead => isBeingDestroyed || currentHealth <= 0f;

    [Tooltip("If enabled, player shots cannot destroy this target. Its normal lifetime still applies.")]
    public bool invincible;

    [Header("Accuracy Settings")]
    [Tooltip("Inner angle (degrees) — how close to the player direction the bullet CAN'T go.")]
    public float innerConeAngle = 2f;

    [Tooltip("Outer angle (degrees) — how far from the player direction the bullet CAN go.")]
    public float outerConeAngle = 10f;

    [Tooltip("Additional horizontal spread in degrees, randomly applied toward the local X-axis.")]
    [Min(0f)]
    public float xAxisSpread;

    [Tooltip("Additional vertical spread in degrees, randomly applied toward the local Y-axis.")]
    [Min(0f)]
    public float yAxisSpread;

    [Header("Firing Settings")]
    [Tooltip("If disabled, this target will not fire bullets.")]
    public bool firesBullets = true;

    [Tooltip("If enabled, bullets aim along the target's local Z-axis instead of toward the player, then apply the configured spread. Targets without a player also use non-tracking fire.")]
    public bool nonTracking;

    [Header("Orientation Settings")]
    [Tooltip("If enabled, the target rotates to face the player when it starts.")]
    public bool facePlayerOnStart = true;

    [Tooltip("Optional pivot that periodically rotates to face the player on tracking targets.")]
    public GameObject barrelPivot;

    [Tooltip("Seconds between barrel aim updates for tracking targets.")]
    [Min(0.02f)]
    public float barrelTrackingInterval = 0.1f;

    [Header("Movement Settings")]
    [Tooltip("How far the target moves in each direction from its starting position. Set to 0 to disable movement.")]
    [Min(0f)]
    public float movementDistance;

    [Tooltip("How fast the target moves in units per second.")]
    [Min(0f)]
    public float movementSpeed;

    [Tooltip("The target's local forward axis. Movement is automatically kept perpendicular to this axis.")]
    public TargetAxis forwardAxis = TargetAxis.Z;

    [Tooltip("If enabled, the target moves left from its starting position first instead of right.")]
    public bool moveLeftFirst;

    public float minTimeUntilShot;
    public float maxTimeUntilShot;

    public GameObject shotIndicator;
    public GameObject bullet;
    float timeUntilNextShot;
    float timeUntilBarrelUpdate;
    float movementPhase;
    Vector3 movementStartLocalPosition;
    Vector3 movementDirectionInParentSpace;
    float currentHealth;

    [HideInInspector]
    public PracticeRangeController prc;
    public GameObject player;
    bool isBeingDestroyed;

    void Awake()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
        currentHealth = maxHealth;
        ResolveReferences();
    }

    void OnValidate()
    {
        maxHealth = Mathf.Max(0.01f, maxHealth);
    }

    // Start is called before the first frame update
    void Start()
    {
        ResolveReferences();
        life = Random.Range(minLife, maxLife);
        if (facePlayerOnStart && !nonTracking && player != null)
        {
            transform.LookAt(player.transform);
        }

        InitializeMovement();

        if (shotIndicator != null)
        {
            shotIndicator.SetActive(firesBullets);
        }

        if (firesBullets)
        {
            timeUntilNextShot = Random.Range(minTimeUntilShot, maxTimeUntilShot);
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (isBeingDestroyed)
        {
            return;
        }

        life -= Time.fixedDeltaTime;
        if (life <= 0)
        {
            HandleTargetRemoval(false);
            return;
        }

        UpdateMovement();

        if (!firesBullets)
        {
            return;
        }

        if (!nonTracking && player != null && barrelPivot != null)
        {
            timeUntilBarrelUpdate -= Time.fixedDeltaTime;
            if (timeUntilBarrelUpdate <= 0f)
            {
                UpdateBarrelAim();
                timeUntilBarrelUpdate = Mathf.Max(barrelTrackingInterval, 0.02f);
            }
        }

        float fireRateDeltaTime = Time.fixedDeltaTime
            * BulletTimeManager.GetScale(BulletTimeChannel.PracticeTargetFireRate);
        timeUntilNextShot -= fireRateDeltaTime;
        if (shotIndicator != null && shotIndicator.activeSelf)
        {
            float safeMinTimeUntilShot = Mathf.Max(minTimeUntilShot, 0.01f);
            shotIndicator.transform.localScale = transform.localScale * (timeUntilNextShot / safeMinTimeUntilShot);
        }

        if (timeUntilNextShot <= 0)
        {
            FireShot();
        }
    }

    public void DestroyTarget()
    {
        if (invincible)
        {
            return;
        }

        currentHealth = 0f;
        HandleTargetRemoval(true);
    }

    public void TakeDamage(DamageInfo damageInfo)
    {
        if (invincible || isBeingDestroyed || damageInfo == null)
        {
            return;
        }

        float damageAmount = Mathf.Max(0f, damageInfo.amount);
        if (damageAmount <= 0f)
        {
            return;
        }

        BodyController sourceBodyController = damageInfo.sourceBodyController;
        bool damagedByPlayer = sourceBodyController != null && !sourceBodyController.isAI;
        AuraManager sourceAuraManager = damagedByPlayer
            ? ResolveSourceAuraManager(sourceBodyController)
            : null;

        sourceAuraManager?.TryRegisterEnemyDamage();
        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
        if (currentHealth > 0f)
        {
            return;
        }

        sourceAuraManager?.TryRegisterEnemyKill();
        HandleTargetRemoval(damagedByPlayer);
    }

    AuraManager ResolveSourceAuraManager(BodyController sourceBodyController)
    {
        if (sourceBodyController.auraManager == null)
        {
            sourceBodyController.auraManager = sourceBodyController.GetComponent<AuraManager>();
        }

        return sourceBodyController.auraManager;
    }

    void FireShot()
    {
        if (bullet == null)
        {
            return;
        }

        Vector3 fireDirection;
        if (nonTracking || player == null)
        {
            // Aim along the target's local Z-axis before applying spread.
            fireDirection = transform.forward;
            fireDirection = ApplyAxisSpread(transform.rotation, fireDirection);
        }
        else
        {
            // Direction from enemy to player
            Vector3 toPlayer = (player.transform.position - transform.position).normalized;

            // Generate a random direction within the hollow cone
            fireDirection = GetRandomDirectionInHollowCone(toPlayer, innerConeAngle, outerConeAngle);
            fireDirection = ApplyAxisSpread(Quaternion.LookRotation(toPlayer), fireDirection);
        }

        // Spawn bullet (pooled)
        Bullet pooledBullet = BulletPool.Spawn(bullet, transform.position, Quaternion.LookRotation(fireDirection), true);
        if (pooledBullet == null)
        {
            var b = Instantiate(bullet, transform.position, Quaternion.LookRotation(fireDirection));
        }

        // Reset timer for next shot
        timeUntilNextShot = Random.Range(minTimeUntilShot, maxTimeUntilShot);
    }

    void UpdateBarrelAim()
    {
        Vector3 barrelToPlayer = player.transform.position - barrelPivot.transform.position;
        if (barrelToPlayer.sqrMagnitude > 0f)
        {
            barrelPivot.transform.rotation = Quaternion.LookRotation(barrelToPlayer);
        }
    }

    void InitializeMovement()
    {
        movementStartLocalPosition = transform.localPosition;

        Vector3 localForward;
        switch (forwardAxis)
        {
            case TargetAxis.X:
                localForward = Vector3.right;
                break;
            case TargetAxis.Y:
                localForward = Vector3.up;
                break;
            default:
                localForward = Vector3.forward;
                break;
        }

        Vector3 referenceAxis = Mathf.Abs(Vector3.Dot(localForward, Vector3.up)) > 0.99f
            ? Vector3.forward
            : Vector3.up;
        Vector3 localMovementDirection = Vector3.Cross(referenceAxis, localForward).normalized;
        movementDirectionInParentSpace = transform.localRotation * localMovementDirection;
    }

    void UpdateMovement()
    {
        float distance = Mathf.Max(movementDistance, 0f);
        float speed = Mathf.Max(movementSpeed, 0f);

        if (distance <= 0f)
        {
            transform.localPosition = movementStartLocalPosition;
            movementPhase = 0f;
            return;
        }

        if (speed <= 0f)
        {
            return;
        }

        float fullCycleDistance = distance * 4f;
        float movementDeltaTime = Time.fixedDeltaTime
            * BulletTimeManager.GetScale(BulletTimeChannel.PracticeTargetMovement);
        movementPhase = Mathf.Repeat(movementPhase + speed * movementDeltaTime, fullCycleDistance);
        float offset = Mathf.PingPong(distance + movementPhase, distance * 2f) - distance;
        if (moveLeftFirst)
        {
            offset = -offset;
        }

        transform.localPosition = movementStartLocalPosition + movementDirectionInParentSpace * offset;
    }

    void HandleTargetRemoval(bool destroyedByPlayer)
    {
        if (isBeingDestroyed)
        {
            return;
        }

        isBeingDestroyed = true;
        ResolvePracticeRangeController();

        if (prc != null)
        {
            if (destroyedByPlayer)
            {
                DestroyedByPlayer?.Invoke(this);
                prc.totalTargetsDestroyed++;
            }

            prc.totalTargetsAccountedFor++;
        }
        else if (destroyedByPlayer)
        {
            DestroyedByPlayer?.Invoke(this);
        }

        Destroy(gameObject);
    }

    void ResolveReferences()
    {
        ResolvePracticeRangeController();

        if (player == null && Camera.main != null)
        {
            player = Camera.main.gameObject;
        }
    }

    void ResolvePracticeRangeController()
    {
        if (prc == null)
        {
            prc = GetComponentInParent<PracticeRangeController>();
        }
    }

    Vector3 GetRandomDirectionInHollowCone(Vector3 forward, float innerAngle, float outerAngle)
    {
        // Random angle between inner and outer cone limits
        float angle = Random.Range(innerAngle, outerAngle);
        float angleRad = angle * Mathf.Deg2Rad;

        // Random rotation around forward axis
        float azimuth = Random.Range(0f, 360f) * Mathf.Deg2Rad;

        // Construct local direction
        Vector3 localDir = new Vector3(
            Mathf.Sin(angleRad) * Mathf.Cos(azimuth),
            Mathf.Sin(angleRad) * Mathf.Sin(azimuth),
            Mathf.Cos(angleRad)
        );

        // Rotate local direction so that it aligns with the given forward direction
        return Quaternion.FromToRotation(Vector3.forward, forward) * localDir;
    }

    Vector3 ApplyAxisSpread(Quaternion aimRotation, Vector3 direction)
    {
        float horizontalSpread = Random.Range(-Mathf.Max(xAxisSpread, 0f), Mathf.Max(xAxisSpread, 0f));
        float verticalSpread = Random.Range(-Mathf.Max(yAxisSpread, 0f), Mathf.Max(yAxisSpread, 0f));

        Vector3 localDirection = Quaternion.Inverse(aimRotation) * direction;
        Quaternion spreadRotation = Quaternion.Euler(-verticalSpread, horizontalSpread, 0f);
        return aimRotation * spreadRotation * localDirection;
    }
}
