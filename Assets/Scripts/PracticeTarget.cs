using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PracticeTarget : MonoBehaviour
{
    public event System.Action<PracticeTarget> DestroyedByPlayer;

    public float life;
    public float minLife;
    public float maxLife;

    [Header("Accuracy Settings")]
    [Tooltip("Inner angle (degrees) — how close to the player direction the bullet CAN'T go.")]
    public float innerConeAngle = 2f;

    [Tooltip("Outer angle (degrees) — how far from the player direction the bullet CAN go.")]
    public float outerConeAngle = 10f;

    [Header("Firing Settings")]
    [Tooltip("If disabled, this target will not fire bullets.")]
    public bool firesBullets = true;

    [Header("Orientation Settings")]
    [Tooltip("If enabled, the target rotates to face the player when it starts.")]
    public bool facePlayerOnStart = true;

    public float minTimeUntilShot;
    public float maxTimeUntilShot;

    public GameObject shotIndicator;
    public GameObject bullet;
    float timeUntilNextShot;

    [HideInInspector]
    public PracticeRangeController prc;
    public GameObject player;
    bool isBeingDestroyed;

    void Awake()
    {
        ResolveReferences();
    }

    // Start is called before the first frame update
    void Start()
    {
        ResolveReferences();
        life = Random.Range(minLife, maxLife);
        if (facePlayerOnStart && player != null)
        {
            transform.LookAt(player.transform);
        }

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

        if (!firesBullets)
        {
            return;
        }

        timeUntilNextShot -= Time.fixedDeltaTime;
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
        HandleTargetRemoval(true);
    }

    void FireShot()
    {
        if (bullet == null || player == null)
        {
            return;
        }

        // Direction from enemy to player
        Vector3 toPlayer = (player.transform.position - transform.position).normalized;

        // Generate a random direction within the hollow cone
        Vector3 fireDirection = GetRandomDirectionInHollowCone(toPlayer, innerConeAngle, outerConeAngle);

        // Spawn bullet (pooled)
        Bullet pooledBullet = BulletPool.Spawn(bullet, transform.position, Quaternion.LookRotation(fireDirection), true);
        if (pooledBullet == null)
        {
            var b = Instantiate(bullet, transform.position, Quaternion.LookRotation(fireDirection));
        }

        // Reset timer for next shot
        timeUntilNextShot = Random.Range(minTimeUntilShot, maxTimeUntilShot);
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
}
