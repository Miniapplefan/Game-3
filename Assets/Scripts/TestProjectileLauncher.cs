using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestProjectileLauncher : MonoBehaviour
{
    public LayerMask ragdollLayerMask;
    public float raycastDistance = 100f;
    public float force = 1000f;
    public float impulseDurationSeconds = 0.5f; // Adjust the duration as needed
    public float fireRate = 0.2f; // Adjust the fire rate as needed
    public Material bulletHoleMaterial; // Assign the material with the bullet hole texture in the inspector

    private bool applyingForce = false;
    private LineRenderer lineRenderer;
    private bool automaticMode = true; // Set this to false for semi-automatic mode

    void Start()
    {
        // Create and configure the Line Renderer
        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Standard"));
        lineRenderer.material.color = Color.blue;
        lineRenderer.enabled = false;
    }

    void Update()
    {
        // Check for user input to initiate firing
        if ((automaticMode && Input.GetMouseButton(0)) || (!automaticMode && Input.GetMouseButtonDown(0)))
        {
            StartCoroutine(Fire());
        }
    }

    IEnumerator Fire()
    {
        while ((automaticMode && Input.GetMouseButton(0)) || (!automaticMode && Input.GetMouseButtonDown(0)))
        {
            // Enable the Line Renderer to visualize the raycast path
            lineRenderer.enabled = true;

            // Cast a ray in the forward direction of the GameObject
            Ray ray = new Ray(transform.position, transform.forward);
            RaycastHit hit;

            // Set the starting point of the line renderer
            lineRenderer.SetPosition(0, transform.position);

            // Check if the ray hits an object within the specified distance and the specified layer mask
            if (Physics.Raycast(ray, out hit, raycastDistance, ragdollLayerMask))
            {
                // Apply impulse force to the object hit
                Rigidbody hitRb = hit.collider.GetComponent<Rigidbody>();

                if (hitRb != null)
                {
                    applyingForce = true;

                    // Set the ending point of the line renderer to the hit point
                    lineRenderer.SetPosition(1, hit.point);

                    // Spawn a bullet hole at the hit point
                    SpawnBulletHole(hit.point, hit.normal, hit.collider.transform);

                    ApplyImpulseForce(hitRb);

                    // Adjust the total duration considering the force application time
                    yield return new WaitForSeconds(impulseDurationSeconds);
                }
            }
            else
            {
                // If the ray doesn't hit anything, set the ending point of the line renderer at the maximum distance
                Vector3 endPosition = transform.position + transform.forward * raycastDistance;
                lineRenderer.SetPosition(1, endPosition);

                // Simulate a visible effect when the ray doesn't hit anything (e.g., change color, scale, etc.)
                // You can add additional visual feedback here.

                // Adjust the total duration
                yield return new WaitForSeconds(impulseDurationSeconds);
            }

            // Disable the Line Renderer when the raycast is finished
            lineRenderer.enabled = false;

            // Introduce a small delay between each shot in automatic mode
            if (automaticMode)
            {
                yield return new WaitForSeconds(fireRate);
            }
        }
    }

    void ApplyImpulseForce(Rigidbody rb)
    {
        Vector3 impulse = transform.forward * force;
        rb.AddForce(impulse, ForceMode.Impulse);
    }

    void SpawnBulletHole(Vector3 position, Vector3 normal, Transform parent)
    {
        // Create a simple quad with a bullet hole texture
        GameObject bulletHole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bulletHole.GetComponent<MeshRenderer>().material = bulletHoleMaterial;
        Destroy(bulletHole.GetComponent<Collider>()); // Remove the collider if interaction is not needed

        // Set the position and rotation based on the hit point and normal
        bulletHole.transform.position = position;
        bulletHole.transform.forward = -normal;
        bulletHole.transform.parent = parent;
    }
}
