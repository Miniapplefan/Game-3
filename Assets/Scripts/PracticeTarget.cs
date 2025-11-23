using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PracticeTarget : MonoBehaviour
{
    public float life;
    public float minLife;
    public float maxLife;

    [Header("Accuracy Settings")]
    [Tooltip("Inner angle (degrees) — how close to the player direction the bullet CAN'T go.")]
    public float innerConeAngle = 2f;

    [Tooltip("Outer angle (degrees) — how far from the player direction the bullet CAN go.")]
    public float outerConeAngle = 10f;

    public float minTimeUntilShot;
    public float maxTimeUntilShot;

    public GameObject shotIndicator;
    public GameObject bullet;
    float timeUntilNextShot;

    [HideInInspector]
    public PracticeRangeController prc;
    public GameObject player;

    // Start is called before the first frame update
    void Start()
    {
        life = Random.Range(minLife, maxLife);
        player = GameObject.Find("Main Camera");

        transform.LookAt(player.gameObject.transform);

        timeUntilNextShot = Random.Range(minTimeUntilShot, maxTimeUntilShot);
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        life -= Time.fixedDeltaTime;
        if (life <= 0)
        {
            prc.totalTargetsAccountedFor++;
            GameObject.Destroy(gameObject);
        }

        timeUntilNextShot -= Time.fixedDeltaTime;
        shotIndicator.transform.localScale = transform.localScale * (timeUntilNextShot / minTimeUntilShot);

        if (timeUntilNextShot <= 0)
        {
            FireShot();
        }
    }

    public void DestroyTarget()
    {
        prc.totalTargetsDestroyed++;
        prc.totalTargetsAccountedFor++;
        GameObject.Destroy(gameObject);
    }

    void FireShot()
    {
        // Instantiate bullet
        var b = Instantiate(bullet, transform.position, Quaternion.identity);

        // Direction from enemy to player
        Vector3 toPlayer = (player.gameObject.transform.position - transform.position).normalized;

        // Generate a random direction within the hollow cone
        Vector3 fireDirection = GetRandomDirectionInHollowCone(toPlayer, innerConeAngle, outerConeAngle);

        // Orient the bullet
        b.transform.rotation = Quaternion.LookRotation(fireDirection);

        // Reset timer for next shot
        timeUntilNextShot = Random.Range(minTimeUntilShot, maxTimeUntilShot);
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
