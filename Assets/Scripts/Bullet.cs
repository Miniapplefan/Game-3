using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    public float speed;
    public MeshRenderer bulletTipMesh;
    public MeshRenderer bulletBodyMesh;
    public Material hitTelegraphMaterial;
    public Material noHitTelegraphMaterial;

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
    }

    private void OnTriggerEnter(Collider other)
    {
        var limb = other.gameObject.GetComponent<LimbToSystemLinker>();
        if (other.gameObject.layer == 6)
        {
            bulletTipMesh.material = hitTelegraphMaterial;
            bulletBodyMesh.material = hitTelegraphMaterial;
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.layer == 6)
        {
            bulletTipMesh.material = hitTelegraphMaterial;
            bulletBodyMesh.material = hitTelegraphMaterial;
        }

    }

    private void OnTriggerExit(Collider other)
    {
        var limb = other.gameObject.GetComponent<LimbToSystemLinker>();
        if (other.gameObject.layer == 6)
        {
            bulletTipMesh.material = noHitTelegraphMaterial;
            bulletBodyMesh.material = noHitTelegraphMaterial;
        }
    }
}
