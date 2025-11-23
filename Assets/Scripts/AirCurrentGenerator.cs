using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AirCurrentGenerator : MonoBehaviour
{

	[Header("Fan Settings")]
	[Tooltip("The maximum positive Z-axis angle the fan will rotate to.")]
	public float positiveAngle = 45f;

	[Tooltip("The maximum negative Z-axis angle the fan will rotate to.")]
	public float negativeAngle = -45f;

	[Tooltip("The time it takes for the fan to rotate from one extreme to the other (in seconds).")]
	public float oscillationSpeed = 2f;

	[Tooltip("The angle at which the fan starts.")]
	public float startAngle = 0f;
	private Quaternion initialRotation;
	private bool rotatingToPositive = true;
	public float baseDissipationRate = 10f;  // Cooling rate of the fan
	public float maxCoolingDistance = 10f;  // Max distance for fan's effect (length of the cooling zone)
	public float coolingZoneRadius = 5f;    // Radius for the cooling zone
	public float minTimeOn = 2f;
	public float maxTimeOn = 6f;
	public float minTimeOff = 10f;
	public float maxTimeOff = 20f;
	private float currentTimeOn;
	private float currentTimeOff;
	public bool isOn;
	//public bool isCoolingSomething;
	private IEnumerator currentCoroutine;

	public GameObject fanBlades;
	public LayerMask heatContainerLayerMask;  // LayerMask to filter only HeatContainers
	public LayerMask obstructionLayerMask;    // Define the layers that can block the cooling effect (e.g., environment, other containers)
	private HeatContainer currentHeatContainer = null;  // The container currently affected by the fan
	private float originalDissipationRate;

	private GameObject coolingZoneObject;
	private AirCurrent airCurrentScript;  // Reference to the generated CoolingZoneScript

	private void Start()
	{
		// Create the cooling zone dynamically
		CreateCoolingZone();
		float rand = Random.Range(0f, 1f);
		if (rand > 0.5)
		{
			currentTimeOn = Random.Range(minTimeOn, maxTimeOn);
			currentCoroutine = StayPoweredOn(currentTimeOn);
			StartCoroutine(currentCoroutine);
		}
		else
		{
			currentTimeOff = Random.Range(minTimeOff, maxTimeOff);
			currentCoroutine = StayPoweredOff(currentTimeOff);
			StartCoroutine(currentCoroutine);
		}

		// Ensure the fan starts at the specified initial angle
		transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, startAngle);
		initialRotation = transform.rotation;

		// Start the oscillation coroutine
		StartCoroutine(Oscillate());
	}

	private void FixedUpdate()
	{
		if (isOn)
		{
			coolingZoneObject.SetActive(true);
			RaycastCoolingCheck();
			DoFanBladeAnimation();
		}
		else
		{
			ResetDissipationRate();
			coolingZoneObject.SetActive(false);
		}
		// if(currentHeatContainer != null)
		// {
		// 	isCoolingSomething = true;
		// }
	}

	private void RaycastCoolingCheck()
	{
		if (!isOn) return;

		Vector3 origin = fanBlades.transform.position;
		Vector3 direction = fanBlades.transform.up;

		RaycastHit hit;
		if (Physics.SphereCast(origin, coolingZoneRadius, direction, out hit, maxCoolingDistance, heatContainerLayerMask))
		{
			GameObject hitObject = hit.collider.gameObject;
			HeatContainer hitContainer = hitObject.GetComponent<HeatContainer>();

			if (hitContainer != null)
			{
				// If we're hitting a new container
				if (hitContainer != currentHeatContainer)
				{
					// Reset the previous one
					if (currentHeatContainer != null)
					{
						ResetDissipationRate();
					}

					// Set the new one and apply cooling
					currentHeatContainer = hitContainer;
					originalDissipationRate = currentHeatContainer.dissipationRateFromAirCurrents;
					ApplyCoolingEffect(currentHeatContainer);
					//Debug.Log("cooling " + hitObject.name);
				}
				// else: we're still hitting the same one, do nothing
			}
			else
			{
				// Ray hit something without a HeatContainer — stop cooling
				if (currentHeatContainer != null)
				{
					ResetDissipationRate();
				}
			}
		}
		else
		{
			// Ray didn't hit anything — stop cooling
			if (currentHeatContainer != null)
			{
				ResetDissipationRate();
			}
		}
	}

	private void DoFanBladeAnimation()
	{
		fanBlades.transform.Rotate(0.0f, 10f, 0f, Space.Self);
	}

	private void CreateCoolingZone()
	{
		// Create a new GameObject to act as the cooling zone
		coolingZoneObject = new GameObject("CoolingZone");
		coolingZoneObject.transform.SetParent(this.transform);  // Set as a child of the fan GameObject
		var rb = coolingZoneObject.AddComponent<Rigidbody>();
		rb.isKinematic = true;

		// Position the cooling zone at the front of the fan (adjust based on fan's forward direction)


		// Add CapsuleCollider to represent the cooling zone
		CapsuleCollider coolingZoneCollider = coolingZoneObject.AddComponent<CapsuleCollider>();
		coolingZoneCollider.isTrigger = true;  // Set collider to trigger
		coolingZoneCollider.providesContacts = true;
		coolingZoneCollider.radius = coolingZoneRadius;
		coolingZoneCollider.height = maxCoolingDistance;
		coolingZoneCollider.direction = 1;  // 2 represents Z-axis direction (forward/backward)

		coolingZoneObject.transform.localPosition = Vector3.up * (maxCoolingDistance / 2);
		coolingZoneObject.transform.rotation = this.transform.rotation;

		// Assign the cooling zone to a different layer (e.g., CoolingZone)
		coolingZoneObject.layer = 11;

		// Add the CoolingZoneScript to the cooling zone GameObject
		airCurrentScript = coolingZoneObject.AddComponent<AirCurrent>();

		// Pass a reference to this FanScript into the CoolingZoneScript
		airCurrentScript.Initialize(this);
	}

	public void OnHeatContainerEntered(HeatContainer newHeatContainer)
	{
		//Debug.Log(newHeatContainer.gameObject.name);
		if (newHeatContainer.isFlammable)
		{
			//			Debug.Log("flammable in cooling zone");
			var flame = newHeatContainer.gameObject.GetComponent<Flammable>();
			if (flame.isOnFire)
			{
				flame.Extinguish();
			}
		}

		if (currentHeatContainer != null)
		{
			ResetDissipationRate();  // Reset the dissipation rate of the previous container
		}

		// Set the new heat container
		currentHeatContainer = newHeatContainer;
		originalDissipationRate = currentHeatContainer.dissipationRateFromAirCurrents;

		// Check if there's a clear path for the fan to cool the container
		if (IsPathClear())
		{
			// Debug.Log("Path clear");
			ApplyCoolingEffect(currentHeatContainer);
		}
	}

	public void OnHeatContainerExited(HeatContainer exitedContainer)
	{
		// Reset the dissipation rate when the HeatContainer leaves the cooling zone
		if (exitedContainer == currentHeatContainer)
		{
			ResetDissipationRate();
			currentHeatContainer = null;
		}
	}

	public bool IsPathClear()
	{
		if (currentHeatContainer == null)
		{
			return false;
		}

		RaycastHit hit;
		Vector3 directionToContainer = (currentHeatContainer.transform.position - fanBlades.transform.position).normalized;

		// Perform the raycast (using the obstructionLayerMask to detect only certain objects)
		if (Physics.Raycast(fanBlades.transform.position, directionToContainer, out hit, maxCoolingDistance))
		{
			// Check if the object hit is NOT the HeatContainer, indicating an obstruction
			if (hit.collider.gameObject != currentHeatContainer.gameObject)
			{
				//Debug.Log("Cooling blocked by " + hit.collider.gameObject.name);
				return false;  // Obstructed
			}
		}

		return true;  // Path is clear
	}

	public void ApplyCoolingEffect(HeatContainer container)
	{
		if (isOn)
		{
			float distanceToFan = Vector3.Distance(transform.position, container.transform.position);
			if (distanceToFan <= maxCoolingDistance)
			{
				float coolingFactor = Mathf.Clamp01(1 - (distanceToFan / maxCoolingDistance));
				float adjustedDissipationRate = baseDissipationRate * coolingFactor;
				// Debug.Log("original " + originalDissipationRate);
				// Debug.Log("with fan " + originalDissipationRate + adjustedDissipationRate);
				container.dissipationRateFromAirCurrents = originalDissipationRate + adjustedDissipationRate;
			}
		}
	}

	private void ResetDissipationRate()
	{
		if (currentHeatContainer != null)
		{
			currentHeatContainer.dissipationRateFromAirCurrents = originalDissipationRate;
			currentHeatContainer = null;
		}
	}

	private IEnumerator StayPoweredOff(float time)
	{
		isOn = false;
		yield return new WaitForSeconds(time);
		isOn = true;
		currentTimeOn = Random.Range(minTimeOn, maxTimeOn);
		currentCoroutine = StayPoweredOn(currentTimeOn);
		StartCoroutine(currentCoroutine);
	}

	private IEnumerator StayPoweredOn(float time)
	{
		isOn = true;
		yield return new WaitForSeconds(time);
		isOn = false;
		currentTimeOff = Random.Range(minTimeOff, maxTimeOff);
		currentCoroutine = StayPoweredOff(currentTimeOff);
		StartCoroutine(currentCoroutine);
	}

	IEnumerator Oscillate()
	{
		while (true) // Infinite loop for continuous oscillation
		{
			Quaternion targetRotation;

			if (rotatingToPositive)
			{
				targetRotation = Quaternion.Euler(initialRotation.eulerAngles.x, initialRotation.eulerAngles.y, startAngle + positiveAngle);
			}
			else
			{
				targetRotation = Quaternion.Euler(initialRotation.eulerAngles.x, initialRotation.eulerAngles.y, startAngle + negativeAngle);
			}

			float timer = 0f;
			Quaternion startLerpRotation = transform.rotation;

			while (timer < oscillationSpeed)
			{
				transform.rotation = Quaternion.Slerp(startLerpRotation, targetRotation, timer / oscillationSpeed);
				timer += Time.deltaTime;
				yield return null; // Wait for the next frame
			}

			// Ensure the fan reaches the exact target rotation
			transform.rotation = targetRotation;

			// Switch direction for the next oscillation
			rotatingToPositive = !rotatingToPositive;

			yield return null; // Wait for one frame before starting the next oscillation
		}
	}
}
