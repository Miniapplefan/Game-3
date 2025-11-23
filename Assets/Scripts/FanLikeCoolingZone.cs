using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FanLikeCoolingZone : MonoBehaviour
{
	public float baseDissipationRate = 1f;    // Full strength of the fan
	public float maxCoolingDistance = 60f;    // Max distance for fan's effect (cylinder length)
	public float radius = 0.5f;                 // Radius of the cooling zone (cylinder radius)
	public float defaultDissipationRate = 1f; // Default dissipation rate for HeatContainers
	public LayerMask obstructionLayerMask;    // Define the layers that can block the cooling effect (e.g., environment, other containers)
	public LayerMask heatContainerLayerMask;  // LayerMask to filter only HeatContainers

	private HeatContainer currentHeatContainer = null;  // The container currently affected by the fan
	private float originalDissipationRate;              // To store the original dissipation rate of the affected container
	private Vector3 fanPosition;

	private CapsuleCollider coolingZone;  // The dynamically created cylindrical collider

	private void Start()
	{
		// Assume the fan's cooling zone direction is along its forward vector
		fanPosition = transform.position;

		// Dynamically create a cylindrical trigger collider at runtime
		CreateCoolingZone();
	}

	private void CreateCoolingZone()
	{
		// Add a CapsuleCollider component
		coolingZone = gameObject.AddComponent<CapsuleCollider>();
		coolingZone.isTrigger = true;  // Set as trigger

		// Set up the collider as a cylinder with height (maxCoolingDistance) and radius
		coolingZone.radius = radius;
		coolingZone.height = maxCoolingDistance;
		coolingZone.direction = 1;  // '2' means it's aligned along the Z axis (forward)

		// Adjust the center so that the cooling zone is in front of the fan
		coolingZone.center = new Vector3(0, -maxCoolingDistance / 2, 0);
	}

	private void OnTriggerEnter(Collider other)
	{
		// Check if the object entering the trigger is a HeatContainer
		HeatContainer newHeatContainer = other.GetComponent<HeatContainer>();

		if (newHeatContainer != null)
		{
			// If there was a previous HeatContainer, reset its dissipation rate
			if (currentHeatContainer != null)
			{
				ResetDissipationRate();
			}

			// Set the new HeatContainer and store its original dissipation rate
			currentHeatContainer = newHeatContainer;
			originalDissipationRate = currentHeatContainer.dissipationRate;

			// Apply cooling effect based on the distance from the fan
			ApplyCoolingEffect(currentHeatContainer);
		}
	}

	private void OnTriggerStay(Collider other)
	{

		// Continuously apply cooling while the container remains inside the cooling zone
		if (currentHeatContainer != null)
		{
			originalDissipationRate = currentHeatContainer.dissipationRate;

			// Check for obstructions using raycasting
			if (IsPathClear(currentHeatContainer))
			{
				ApplyCoolingEffect(currentHeatContainer);
			}
		}
	}

	private void OnTriggerExit(Collider other)
	{
		// Check if the object exiting is the current HeatContainer
		HeatContainer exitingContainer = other.GetComponent<HeatContainer>();
		if (exitingContainer != null && exitingContainer == currentHeatContainer)
		{
			// Reset the dissipation rate of the current container and clear the reference
			ResetDissipationRate();
			currentHeatContainer = null;
		}
	}

	private bool IsPathClear(HeatContainer container)
	{
		// Cast a ray from the fan's position to the HeatContainer to check for obstructions
		RaycastHit hit;
		Vector3 directionToContainer = (container.transform.position - fanPosition).normalized;

		// Perform the raycast (using the obstructionLayerMask to detect only certain objects)
		if (Physics.Raycast(fanPosition, directionToContainer, out hit, maxCoolingDistance, obstructionLayerMask | heatContainerLayerMask))
		{
			// Check if the object hit is NOT the HeatContainer, indicating an obstruction
			if (hit.collider.gameObject != container.gameObject)
			{
				//Debug.Log("Cooling blocked by " + hit.collider.gameObject.name);
				return false;  // Obstructed
			}
		}

		return true;  // Path is clear
	}

	private void ApplyCoolingEffect(HeatContainer container)
	{
		Debug.Log("Fan is coolin");

		// Calculate the distance from the fan's position to the HeatContainer
		float distanceToFan = Vector3.Distance(fanPosition, container.transform.position);

		// If the object is within range and inside the cooling zone
		if (distanceToFan <= maxCoolingDistance)
		{
			// Apply a dissipation rate that decreases with distance
			float coolingFactor = Mathf.Clamp01(1 - (distanceToFan / maxCoolingDistance));
			float adjustedDissipationRate = baseDissipationRate * coolingFactor;

			// Apply the adjusted dissipation rate to the container
			container.dissipationRate = originalDissipationRate + adjustedDissipationRate;
		}
	}

	private void ResetDissipationRate()
	{
		// Reset the dissipation rate to its original value
		if (currentHeatContainer != null)
		{
			currentHeatContainer.dissipationRate = originalDissipationRate;
		}
	}

	private void OnDrawGizmos()
	{
		// Draw the cylindrical cooling zone for visualization (optional)
		Gizmos.color = Color.blue;
		Gizmos.DrawWireSphere(transform.position + transform.up * maxCoolingDistance, radius);
	}
}
