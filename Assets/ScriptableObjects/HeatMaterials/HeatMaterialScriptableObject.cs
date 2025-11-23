using System.Collections;
using UnityEngine;
//using UnityEngine.Pool;
using Lean.Pool;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HeatMateral", menuName = "HeatMaterials/HeatMaterial", order = 0)]
public class HeatMaterialScriptableObject : ScriptableObject
{
	public HeatContainer.ContainerType containerType;
	public float specificHeatCapacity;     // Specific heat capacity, different for each type
	public float mass = 1f;                // Mass of the object (could be volume for air/water)
	public float fluidInteractionConstant;
	public float thermalConductivity;
	public float ignitionTemperature;
	public float fuelBurnRate = 0.001f;
	public float burnTemperatureMultiplier = 2;
	public float radiationConstant = 0.5f;
}
