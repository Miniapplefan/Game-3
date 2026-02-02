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

    [SerializeField] LayerMask playerMask;
    bool shouldTelegraph = false;
    bool isTelegraph = false;

    [SerializeField] private float lifetime = 10f; // Seconds before the bullet is destroyed

    // Start is called before the first frame update
    void Start()
    {
        bulletTipMesh.material = noHitTelegraphMaterial;
        bulletBodyMesh.material = noHitTelegraphMaterial;
        Destroy(gameObject, lifetime);
    }

    // Update is called once per frame
    void Update()
    {
        transform.position += transform.forward * speed * Time.deltaTime;

        if (shouldTelegraph && !isTelegraph)
        {
            bulletTipMesh.material = hitTelegraphMaterial;
            bulletBodyMesh.material = hitTelegraphMaterial;
            trail.material = hitTelegraphMaterial;
            isTelegraph = true;
        }

        if (isTelegraph && !shouldTelegraph)
        {
            bulletTipMesh.material = noHitTelegraphMaterial;
            bulletBodyMesh.material = noHitTelegraphMaterial;
            trail.material = noHitTelegraphMaterial;
            isTelegraph = false;
        }
    }

    void FixedUpdate()
    {
        if (isTelegraph && shouldTelegraph)
        {
            if (Physics.SphereCast(transform.position, 0.1f, transform.forward, out RaycastHit hit, 0.1f, playerMask, QueryTriggerInteraction.Ignore))
            {
                // Damage
                if (hit.collider.GetComponentInParent<PlayerController>() != null)
                {
                    hit.collider.GetComponentInParent<BodyController>().Die();
                }
                Destroy(gameObject);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var limb = other.gameObject.GetComponent<LimbToSystemLinker>();
        if (other.gameObject.layer == 6)
        {
            shouldTelegraph = true;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        // if (other.gameObject.layer == 6)
        // {
        //     bulletTipMesh.material = hitTelegraphMaterial;
        //     bulletBodyMesh.material = hitTelegraphMaterial;
        // }
    }

    private void OnTriggerExit(Collider other)
    {
        var limb = other.gameObject.GetComponent<LimbToSystemLinker>();
        if (other.gameObject.layer == 6)
        {
            shouldTelegraph = false;
        }
    }
}
