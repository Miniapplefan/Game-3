using UnityEngine;

public class BulletTester : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject bulletPrefab;
    public Transform spawnPoint;
    public float bulletsPerSecond = 10f;

    [Header("Control")]
    public bool isActive = true;
    public bool useFixedUpdate = false;

    float nextFireTime;

    void Reset()
    {
        spawnPoint = transform;
    }

    void Update()
    {
        if (useFixedUpdate)
        {
            return;
        }

        Tick(Time.time);
    }

    void FixedUpdate()
    {
        if (!useFixedUpdate)
        {
            return;
        }

        Tick(Time.time);
    }

    void Tick(float timeNow)
    {
        if (!isActive || bulletPrefab == null)
        {
            return;
        }

        if (bulletsPerSecond <= 0f)
        {
            return;
        }

        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }

        if (timeNow < nextFireTime)
        {
            return;
        }

        nextFireTime = timeNow + (1f / bulletsPerSecond);
        Bullet pooledBullet = BulletPool.Spawn(bulletPrefab, spawnPoint.position, spawnPoint.rotation, true);
        if (pooledBullet == null)
        {
            Instantiate(bulletPrefab, spawnPoint.position, spawnPoint.rotation);
        }
    }

    void OnDrawGizmosSelected()
    {
        Transform source = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(source.position, source.position + source.forward * 3f);
        Gizmos.DrawSphere(source.position, 0.05f);
    }
}
