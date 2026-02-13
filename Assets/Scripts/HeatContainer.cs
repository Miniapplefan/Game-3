using UnityEngine;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using System.Collections;
using System.Linq;

public class HeatContainer : MonoBehaviour
{
	public enum ContainerType { Water, Air, Structural, Solid }
	public ContainerType containerType;

	public HeatMaterialScriptableObject heatMat;

	public float currentTemperature = 21f; // Current heat level
	public float maxTemperature = 1000; // Max heat capacity
	public float specificHeatCapacity;     // Specific heat capacity, different for each type
	public float mass = 1f;                // Mass of the object (could be volume for air/water)
	public float fluidInteractionConstant;
	public float thermalConductivity;
	public HeatContainer currentAir;
	public AirGrid airGrid;
	Collider heatCollider;
	//HeatContainer me;
	public bool isBeingFlamed;
	public HeatMaterialScriptableObject structuralMaterial;
	public float ambientTemperature = 21f; // Ambient temp for dissipation
	public float dissipationRateFromAirCurrents = 1f; // Dissipation rate from air currents 
	public float dissipationRate = 1.0f; // Dissipation rate (passive or for heat transfer)
	public CoolingModel coolingModel; // Cooling model for mechs
	public bool isInTransferZone = false; // Tracks if this object is in a heat transfer zone
	public bool isFlammable = false;
	public bool shouldApplyRadiativeHeating = false;
	public HeatBubble heatBubble;
	public event Action OnOverheated; // Overheat event (for mechs)

	public List<HeatContainer> transferTargets = new List<HeatContainer>(); // For multi-way transfer

	float coolingConstant = 0.02f;

	void Awake()
	{
		if (containerType == ContainerType.Structural)
		{
			return;
		}
		heatCollider = GetComponent<Collider>();

		InitFromHeatMaterialSO();
		// If a mech with a cooling model, initialize
		if (containerType == ContainerType.Air)
		{
			createStruturalHeatContainer();
		}
	}

	// Method to set specific heat capacity based on container type
	public void InitFromHeatMaterialSO()
	{
		if (heatMat == null)
		{
			Debug.LogWarning("No HeatMaterialScriptableObject set for " + gameObject.name + " if this is a Structural HeatContainer ignore this");
			return;
		}
		containerType = heatMat.containerType;
		specificHeatCapacity = heatMat.specificHeatCapacity;
		mass = CalculateMass();
		fluidInteractionConstant = heatMat.fluidInteractionConstant;
		thermalConductivity = heatMat.thermalConductivity;

		if (GetComponent<Flammable>() != null)
		{
			isFlammable = true;
		}
	}

	public void InitCoolingModel(CoolingModel model)
	{
		coolingModel = model;
		// maxTemperature = coolingModel.GetMaxHeat();
		coolingModel.OnCoolingStateChanged += () => OnCoolingStateChanged(coolingModel.currentCoolingState);
		OnCoolingStateChanged(coolingModel.currentCoolingState); // Initialize dissipation rate
		mass = heatMat.mass;
	}

	void Start()
	{
		if (coolingModel == null)
		{
			CalculateMass();
		}

		Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1f);  // Radius can be adjusted
		foreach (var hitCollider in hitColliders)
		{
			HeatContainer otherHeatContainer = hitCollider.GetComponent<HeatContainer>();
			if (otherHeatContainer != null && !transferTargets.Contains(otherHeatContainer) && otherHeatContainer != this)
			{
				if (containerType == ContainerType.Solid && otherHeatContainer.containerType == ContainerType.Solid) continue;

				transferTargets.Add(otherHeatContainer);
				if (otherHeatContainer.containerType == ContainerType.Air)
				{
					// currentAir = otherHeatContainer;
					// ambientTemperature = currentAir.currentTemperature;
				}
				// if (containerType == ContainerType.Water)
				// {
				// 	Debug.Log("I, " + containerType + " am now exchanging with " + otherHeatContainer.containerType);
				// }
			}
		}
		//		airGrid = GameObject.Find("AirGrid").GetComponent<AirGrid>();

		// if (isFlammable)
		// {
		// 	StartCoroutine("CheckForHeatBubble");
		// }
	}

	// Calculate max heat capacity based on volume of collider (optional for different container types)
	float CalculateMass()
	{
		if (containerType == ContainerType.Structural)
		{
			return 0;
			// return currentAir.GetColliderVolume(currentAir.GetComponent<BoxCollider>()) * heatMat.mass;
		}

		float volume = GetColliderVolume(heatCollider);
		return volume * heatMat.mass; // Adjust scale for heat capacity
																	//Debug.Log("Calculated mass for: " + heatMat.containerType + " = " + mass);
	}

	void createStruturalHeatContainer()
	{
		var structuralObject = new GameObject("Structure");
		structuralObject.AddComponent<BoxCollider>();
		structuralObject.AddComponent<HeatContainer>();
		var structuralHeatCont = structuralObject.GetComponent<HeatContainer>();
		structuralHeatCont.heatMat = structuralMaterial;
		structuralHeatCont.currentAir = this;
		structuralHeatCont.InitFromHeatMaterialSO();

		transferTargets.Add(structuralObject.GetComponent<HeatContainer>());
	}

	void FixedUpdate()
	{
		//DissipateHeat();
		// foreach (HeatContainer target in transferTargets)
		// {
		// 	//Debug.Log(gameObject.name + " transferring heat to " + target.gameObject.name);
		// 	TransferHeat(target);
		// }

		// TransferHeatToAir();
		// Always transfer heat if there are targets
		// if (coolingModel != null)
		// {
		// 	Debug.Log(dissipationRate);
		// }
	}

	// Called when cooling state changes (for mechs)
	private void OnCoolingStateChanged(CoolingModel.CoolingState state)
	{
		//Debug.Log("Changing dissipation rate");
		if (coolingModel != null)
		{
			switch (state)
			{
				case CoolingModel.CoolingState.PassiveCooldown:
					dissipationRate = 1;
					break;
				case CoolingModel.CoolingState.Cooldown:
					dissipationRate = coolingModel.GetCooldownMultiplier();
					break;
				case CoolingModel.CoolingState.CooldownOverheated:
					dissipationRate = coolingModel.GetOverheatedCoolingMultiplier();
					break;
				default:
					dissipationRate = 1;
					break;
			}
		}
	}

	// Newton's Law of Cooling
	float GetPassiveCoolingRate()
	{
		//float temperatureDifference = currentHeat - ambientTemperature;
		//float coolingConstant = 0.02f; // Tune this value for desired cooling effect
		//return coolingConstant * temperatureDifference;
		return 0;
	}

	// Method to calculate temperature from current heat
	public float GetTemperature()
	{
		return currentTemperature;
	}

	// Dissipate heat into the environment or air
	// void DissipateHeat()
	// {
	// 	//Debug.Log(dissipationRate);

	// 	if (currentTemperature > ambientTemperature)
	// 	{

	// 		float totalCoolingRate = 0;

	// 		// If mech, also apply active cooling
	// 		if (coolingModel != null)
	// 		{
	// 			totalCoolingRate += dissipationRate;
	// 		}

	// 		// Apply cooling and clamp to ambient temperature
	// 		currentHeat -= totalCoolingRate * Time.deltaTime;
	// 		currentHeat = Mathf.Max(currentHeat, ambientTemperature);
	// 	}

	// 	// Handle overheating for mechs
	// 	if (coolingModel != null)
	// 	{
	// 		coolingModel.HandleHeatExtremes(currentHeat, maxHeatCapacity);
	// 	}
	// }

	IEnumerator CheckForHeatBubble()
	{
		yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.5f));
		ApplyRadiativeHeating();
		StartCoroutine("CheckForHeatBubble");
	}

	void ApplyRadiativeHeating()
	{
		if (shouldApplyRadiativeHeating)
		{
			if (heatBubble != null)
			{
				return;
			}
			else
			{
				Collider[] colliders = Physics.OverlapSphere(transform.position, 1f);
				foreach (var col in colliders)
				{
					if (col.TryGetComponent(out HeatBubble target))
					{
						heatBubble = target;
						target.AddContributer(gameObject.GetComponent<HeatContainer>(), true);
						//if (gameObject.name == "Wood Log (9)") Debug.Log("joining heatbubble " + target.gameObject.name);
						return;
					}
				}
			}
			//if (gameObject.name == "Wood Log (9)") Debug.Log("no heatbubble found");
			createHeatBubble();
		}
	}

	public void createHeatBubble()
	{
		var heatBubbleObject = new GameObject("HeatBubble");

		// Add required components
		var sphereCollider = heatBubbleObject.AddComponent<SphereCollider>();
		sphereCollider.isTrigger = true;

		heatBubbleObject.AddComponent<HeatBubble>();
		heatBubble = heatBubbleObject.GetComponent<HeatBubble>();
		heatBubble.bubbleStarter = GetComponent<Flammable>();
		heatBubble.AddContributer(this, true);

		// Set transform to world position and rotation of the parent
		heatBubbleObject.transform.position = Vector3.zero;
		heatBubbleObject.transform.rotation = Quaternion.identity;
		heatBubbleObject.transform.localScale = new Vector3(1 / this.transform.localScale.x, 1 / this.transform.localScale.y, 1 / this.transform.localScale.z);

		// Parent it without preserving world transform — this keeps it unskewed
		heatBubbleObject.transform.SetParent(this.transform, false);
	}

	// Handles multi-way heat transfer
	public void TransferHeat(HeatContainer otherContainer)
	{
		/*
			if (containerType == ContainerType.Air || otherContainer.containerType == ContainerType.Air)
			{
				//Debug.Log(gameObject.name + " is transferring to " + otherContainer.gameObject.name);

				// Use Newton's Law of Cooling for the air
				ApplyNewtonsLawOfCooling(otherContainer);
			}
			else
			{
				// Use the conduction model for heat transfer between mechs and water
				ApplyConductionModel(otherContainer);
			}
			*/

		if (containerType == ContainerType.Water)
		{
			ApplyConductionModel(otherContainer);
		}
	}

	void TransferHeatToAir()
	{
		if (heatCollider == null)
		{
			return;
		}

		if (!TryResolveAirGrid(out AirGrid grid))
		{
			return;
		}

		List<Vector3Int> cubes = grid.GetCollidingAirCubes(heatCollider);
		if (cubes == null || cubes.Count == 0)
		{
			return;
		}
		float totalTemp = 0;

		foreach (var item in cubes)
		{
			grid.ApplyNewtonsLawOfCooling(this, cubes.Count, item.x, item.y, item.z);
			totalTemp += grid.GetTemperature(item.x, item.y, item.z);
		}

		ambientTemperature = totalTemp / cubes.Count;
	}

	bool TryResolveAirGrid(out AirGrid grid)
	{
		if (airGrid != null)
		{
			grid = airGrid;
			return true;
		}

		grid = FindObjectOfType<AirGrid>();
		airGrid = grid;
		return grid != null;
	}

	private void ApplyNewtonsLawOfCooling(HeatContainer otherContainer)
	{
		// Ensure only one of them is Air (because Newton's law applies between a body and ambient air)
		if (containerType == ContainerType.Air || otherContainer.containerType == ContainerType.Air)
		{
			HeatContainer airContainer = (containerType == ContainerType.Air) ? this : otherContainer;
			HeatContainer bodyContainer = (containerType == ContainerType.Air) ? otherContainer : this;

			// Get the temperatures of both containers
			float airTemperature = airContainer.GetTemperature();
			float bodyTemperature = bodyContainer.GetTemperature();

			// Calculate the temperature difference
			float temperatureDifference = isBeingFlamed ? 1 : bodyTemperature - airTemperature;

			// Mech overheating case
			if (coolingModel != null && coolingModel.isOverheated)
			{
				// Mech is overheated and should dissipate heat below ambient temperature until it reaches its minimumTemperature
				float minTemperature = coolingModel.minimumTemperature;
				//temperatureDifference = bodyTemperature - Mathf.Min(airTemperature, minTemperature); // Allow cooling down to minTemperature

				// Apply Newton's law: heatTransfer = coolingConstant * (temp difference) * dissipationRate * Time.deltaTime
				float heatTransfer = GetCoolingConstant(airContainer.heatMat) * (dissipationRate) * Time.deltaTime;

				// Calculate the temperature change for the body and air based on their specific heat capacity and mass
				float bodyTempChange = heatTransfer / (bodyContainer.mass * bodyContainer.specificHeatCapacity);
				float airTempChange = heatTransfer / (airContainer.mass * airContainer.specificHeatCapacity);

				// Exchange heat
				bodyContainer.IncreaseHeat(airContainer.gameObject, -heatTransfer);
				airContainer.IncreaseHeat(airContainer.gameObject, heatTransfer);

				// Update temperatures
				// bodyContainer.currentTemperature -= bodyTempChange;
				// airContainer.currentTemperature += airTempChange;

				// Clamp the body temperature to the minimumTemperature to prevent overcooling below the limit
				currentTemperature = Mathf.Max(bodyContainer.currentTemperature, minTemperature);

			}
			else
			{
				if (Mathf.Abs(temperatureDifference) > 0.01f)  // Only transfer heat if there's a significant difference
				{
					// Determine which way the heat flows
					//dissipationRateFromAirCurrents
					float heatTransfer = GetCoolingConstant(airContainer.heatMat) * dissipationRateFromAirCurrents * Mathf.Abs(temperatureDifference) * Time.deltaTime;

					//					Debug.Log(heatTransfer);

					// If the Objects is hotter than the Air, heat should flow from the Object to the Air
					if (temperatureDifference > 0)
					{
						// Mechs have active cooling which allows them to dissipate heat faster into the air
						heatTransfer *= bodyContainer.dissipationRate;
						// Heat flows from Object to Air
						float bodyTempChange = heatTransfer / (bodyContainer.mass * bodyContainer.specificHeatCapacity);
						float airTempChange = heatTransfer / (airContainer.mass * airContainer.specificHeatCapacity);

						// Exchange heat
						bodyContainer.IncreaseHeat(airContainer.gameObject, -heatTransfer);
						airContainer.IncreaseHeat(airContainer.gameObject, heatTransfer);

						// // Update temperatures
						// bodyContainer.currentTemperature -= bodyTempChange;
						// airContainer.currentTemperature += airTempChange;
					}
					else
					{
						// Heat flows from Air to Object (Air is hotter)
						float bodyTempChange = heatTransfer / (bodyContainer.mass * bodyContainer.specificHeatCapacity);
						float airTempChange = heatTransfer / (airContainer.mass * airContainer.specificHeatCapacity);

						// Exchange heat
						bodyContainer.IncreaseHeat(airContainer.gameObject, heatTransfer);
						airContainer.IncreaseHeat(airContainer.gameObject, -heatTransfer);

						// // Update temperatures
						// bodyContainer.currentTemperature += bodyTempChange;
						// airContainer.currentTemperature -= airTempChange;
					}

					// Ensure temperatures remain within realistic bounds
					bodyContainer.currentTemperature = Mathf.Max(bodyContainer.currentTemperature, airContainer.ambientTemperature);  // Prevent Mechs from cooling below ambient unless overheated
					airContainer.currentTemperature = Mathf.Max(airContainer.currentTemperature, 0);  // Prevent Air from going below 0 (or any desired minimum)
				}
			}

			// Optionally handle any extreme heat conditions (such as additional cooling effects)
			if (coolingModel != null)
			{
				coolingModel.HandleHeatExtremes(bodyContainer.currentTemperature, bodyContainer.maxTemperature);
			}
		}
	}

	public float GetCoolingConstant(HeatMaterialScriptableObject fluidMaterial)
	{
		// Determine which fluid/material interaction is happening
		float interactionConstant = fluidMaterial.fluidInteractionConstant;

		// Calculate coolingConstant based on thermal conductivity and interaction
		return interactionConstant * Mathf.Sqrt(thermalConductivity);
	}

	private void ApplyConductionModel(HeatContainer otherContainer)
	{
		float thisTemperature = GetTemperature();
		float otherTemperature = otherContainer.GetTemperature();

		// Calculate the temperature difference
		float temperatureDifference = Mathf.Abs(thisTemperature - otherTemperature);
		if (temperatureDifference > 1)  // Only proceed if there's a temperature difference
		{
			// Apply conduction heat transfer: heatTransfer = transferRate * (temp difference) * Time.deltaTime
			float heatTransfer = CalculateTransferRate(this, otherContainer) * temperatureDifference * Time.deltaTime;

			if (thisTemperature > otherTemperature)  // Transfer from this container to the other
			{
				float tempChangeThis = heatTransfer / (mass * specificHeatCapacity);
				float tempChangeOther = heatTransfer / (otherContainer.mass * otherContainer.specificHeatCapacity);

				// Exchange heat
				IncreaseHeat(otherContainer, -heatTransfer);
				otherContainer.IncreaseHeat(this, heatTransfer);

				// // Update the temperatures of both containers
				// currentTemperature -= tempChangeThis;
				// otherContainer.currentTemperature += tempChangeOther;
			}
			else if (thisTemperature < otherTemperature)  // Transfer from the other container to this one
			{
				float tempChangeThis = heatTransfer / (mass * specificHeatCapacity);
				float tempChangeOther = heatTransfer / (otherContainer.mass * otherContainer.specificHeatCapacity);

				// Exchange heat
				IncreaseHeat(otherContainer, heatTransfer);
				otherContainer.IncreaseHeat(this, -heatTransfer);

				// Update the temperatures of both containers
				// currentTemperature += tempChangeThis;
				// otherContainer.currentTemperature -= tempChangeOther;
			}
			if (coolingModel != null)
			{
				coolingModel.HandleHeatExtremes(currentTemperature, maxTemperature);
			}
		}
	}

	private float CalculateTransferRate(HeatContainer container1, HeatContainer container2)
	{
		// Approximate contact area based on mass (simple assumption)
		//float contactArea = Mathf.Pow(container1.mass, 2.0f / 3.0f) + Mathf.Pow(container2.mass, 2.0f / 3.0f);

		// Calculate transfer rate based on thermal conductivity and contact area
		return (container1.thermalConductivity + container2.thermalConductivity) / 2.0f;
	}

	public float GetAirTemperature()
	{
		return ambientTemperature;
	}

	//Old GetAirTemperature that the AI used
	public float GetAirTemperatureLegacy()
	{
		return currentAir != null ? currentAir.GetTemperature() : GetTemperature();
	}

	public float GetTemperatureRelativeToAir()
	{
		return currentTemperature - GetAirTemperature();
	}

	// Method to add a transfer target when entering heat transfer zone
	private void OnTriggerEnter(Collider other)
	{
		if (shouldApplyRadiativeHeating) return;
		HeatContainer otherHeatContainer = other.GetComponent<HeatContainer>();
		if (otherHeatContainer != null && !transferTargets.Contains(otherHeatContainer))
		{
			//Debug.Log("I, " + gameObject.name + " transferring to " + other.gameObject.name);

			transferTargets.Add(otherHeatContainer);
		}
	}

	// Method to remove transfer target when exiting heat transfer zone
	private void OnTriggerExit(Collider other)
	{
		HeatContainer otherHeatContainer = other.GetComponent<HeatContainer>();
		if (otherHeatContainer != null && transferTargets.Contains(otherHeatContainer))
		{
			transferTargets.Remove(otherHeatContainer);
		}
	}

	public float GetColliderVolume(Collider collider)
	{
		if (collider.GetType() == typeof(BoxCollider))
		{
			BoxCollider b = (BoxCollider)collider;
			Vector3 worldSize = Vector3.Scale(b.size, transform.lossyScale);
			//Debug.Log("box volume for: " + collider.gameObject.name + " is " + b.size.x * b.size.y * b.size.z);
			return worldSize.x * worldSize.y * worldSize.z;
		}
		else if (collider.GetType() == typeof(SphereCollider))
		{
			SphereCollider s = (SphereCollider)collider;
			//Debug.Log("sphere volume for: " + collider.gameObject.name + " is " + (4f / 3f) * Mathf.PI * Mathf.Pow(s.radius, 3));
			return (4f / 3f) * Mathf.PI * Mathf.Pow(s.radius, 3);
		}
		//Debug.Log("default volume for: " + collider.gameObject.name);
		return 1f; // Default to 1 if no volume
	}

	// Increase heat method (for laser hits, etc.)
	public void IncreaseHeat(object sender, float amount)
	{
		// Check for overheating based on temperature
		if (currentTemperature >= maxTemperature && OnOverheated != null)
		{
			// OnOverheated?.Invoke();
		}
		if (coolingModel != null && coolingModel.isOverheated && amount > 0) return;

		if (isFlammable && currentTemperature >= heatMat.ignitionTemperature * heatMat.burnTemperatureMultiplier && amount > 0)
		{
			return;
		}

		// float thermalInertia = Mathf.Sqrt(heatMat.thermalConductivity * heatMat.mass * heatMat.specificHeatCapacity);
		// float thermalInertiaScalingFactor = thermalInertia / 1000; // normalize around a baseline

		float temperatureChange = amount / (mass * specificHeatCapacity);
		currentTemperature += temperatureChange;
		currentTemperature = Mathf.Clamp(currentTemperature, 0, 1000);
		if (isFlammable)
		{
			currentTemperature = Mathf.Clamp(currentTemperature, 0, heatMat.ignitionTemperature * heatMat.burnTemperatureMultiplier);
		}
	}
}
