using CrashKonijn.Goap.Behaviours;
using UnityEngine;
using Unity.Mathematics;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System;

[RequireComponent(typeof(AgentBehaviour))]
public class NPCBrain : MonoBehaviour
{
	private AgentBehaviour AgentBehaviour;
	public BodyState bodyState;
	public float currentGoalInertia;
	public float maxInertia = 0.5f;
	public float lastDecisionTime = 0;
	public List<GoalConsideration> goals;

	public float ConsiderCooldownVal;
	public float ConsiderOverheatTargetVal;
	public float ConsiderDeploySiphonVal;
	public float ConsiderDefendSiphonVal;
	public float ConsiderTakeCoverVal;


	private void Awake()
	{
		AgentBehaviour = GetComponent<AgentBehaviour>();
		bodyState = GetComponentInChildren<BodyState>();

		goals = new List<GoalConsideration>(); // Initialize the list
																					 //goals.Add(new GoalConsideration(new CooldownGoal(), true, ConsiderCooldownGoal));
		goals.Add(new GoalConsideration(new OverheatHostileGoal(), true, ConsiderOverheatTargetGoal));
		//goals.Add(new GoalConsideration(new DeploySiphonGoal(), true, ConsiderDeploySiphonGoal));
		//goals.Add(new GoalConsideration(new DefendSiphonGoal(), true, ConsiderDefendSiphonGoal));
		goals.Add(new GoalConsideration(new TakeCoverGoal(), true, ConsiderTakeCoverGoal));
	}

	private void Start()
	{
		AgentBehaviour.SetGoal<OverheatHostileGoal>(false);
	}

	private void FixedUpdate()
	{
		//ConsiderCooldownVal = ConsiderCooldownGoal();
		ConsiderOverheatTargetVal = ConsiderOverheatTargetGoal();
		//ConsiderDeploySiphonVal = ConsiderDeploySiphonGoal();
		//ConsiderDefendSiphonVal = ConsiderDefendSiphonGoal();
		ConsiderTakeCoverVal = ConsiderTakeCoverGoal();

		// if (bodyState.targetBodyState != null && bodyState.targetBodyState.Cooling_IsOverheated())
		// {
		// 	AgentBehaviour.SetGoal<OverheatHostileGoal>(false);
		// 	currentGoalInertia = maxInertia;
		// }

		//else if (currentGoalInertia <= 0)
		// else
		// {
		// 	if (ConsiderDeploySiphonGoal() > ConsiderCooldownGoal())
		// 	{
		// 		AgentBehaviour.SetGoal<DeploySiphonGoal>(false);
		// 		//currentGoalInertia = (ConsiderDeploySiphonGoal() - ConsiderCooldownGoal()) * maxInertia;
		// 	}
		// 	else if (ConsiderCooldownGoal() > ConsiderOverheatTargetGoal())
		// 	{
		// 		AgentBehaviour.SetGoal<CooldownGoal>(false);
		// 		//currentGoalInertia = (ConsiderCooldownGoal() - ConsiderOverheatTargetGoal()) * maxInertia;
		// 	}
		// 	else
		// 	{
		// 		AgentBehaviour.SetGoal<OverheatHostileGoal>(false);
		// 		//currentGoalInertia = (ConsiderOverheatTargetGoal() - ConsiderCooldownGoal()) * maxInertia;
		// 	}
		// }
		// currentGoalInertia -= Time.deltaTime;

		// else
		// {
		if (currentGoalInertia <= 0)
		{
			GoalConsideration chosenGoal = GetHighestConsiderationGoal(goals);
			switch (chosenGoal.goal)
			{
				case OverheatHostileGoal:
					AgentBehaviour.SetGoal<OverheatHostileGoal>(chosenGoal.cancelable);
					currentGoalInertia = Mathf.Clamp(chosenGoal.Consideration(), 0, maxInertia);
					break;
				case TakeCoverGoal:
					AgentBehaviour.SetGoal<TakeCoverGoal>(chosenGoal.cancelable);
					currentGoalInertia = Mathf.Clamp(chosenGoal.Consideration(), 0, maxInertia);
					break;
				case DeploySiphonGoal:
					AgentBehaviour.SetGoal<DeploySiphonGoal>(chosenGoal.cancelable);
					currentGoalInertia = Mathf.Clamp(chosenGoal.Consideration(), 0, maxInertia);
					break;
				case DefendSiphonGoal:
					AgentBehaviour.SetGoal<DefendSiphonGoal>(chosenGoal.cancelable);
					currentGoalInertia = Mathf.Clamp(chosenGoal.Consideration(), 0, maxInertia);
					break;
				case CooldownGoal:
					AgentBehaviour.SetGoal<CooldownGoal>(chosenGoal.cancelable);
					currentGoalInertia = Mathf.Clamp(chosenGoal.Consideration(), 0, maxInertia);
					break;
				default:
					AgentBehaviour.SetGoal<TakeCoverGoal>(chosenGoal.cancelable);
					break;
			}
			//DebugLogGoal(chosenGoal.goal);
		}
		//}
		currentGoalInertia -= Time.deltaTime;
	}

	public struct GoalConsideration
	{
		public GoalBase goal;
		// decides if the ongoing action should terminate before setting the new goal.
		public bool cancelable;
		public Func<float> considerationFunction;

		public GoalConsideration(GoalBase goal, bool cancelable, Func<float> considerationFunction)
		{
			this.goal = goal;
			this.cancelable = cancelable;
			this.considerationFunction = considerationFunction;
		}

		public float Consideration()
		{
			return considerationFunction();
		}
	}

	private GoalConsideration GetHighestConsiderationGoal(List<GoalConsideration> goals)
	{
		if (goals == null || goals.Count == 0)
		{
			throw new ArgumentException("The list of goals cannot be null or empty");
		}

		return goals.Aggregate((maxGoal, currentGoal) =>
				currentGoal.Consideration() > maxGoal.Consideration() ? currentGoal : maxGoal);
	}

	private float FormatConsiderationVal(float val)
	{
		return Mathf.Round(Mathf.Clamp01(val) * 100f) / 100f;
	}

	private float ConsiderCooldownGoal()
	{

		return FormatConsiderationVal(
		LegsSystemActiveConsideration() *
		// AirTemperatureConsideration() *
		PositiveHeatConsideration(0.7f)
		// * FarFromTargetConsideration()
		);
		//* weaponsChargedConsideration
		;
	}

	private float ConsiderTakeCoverGoal()
	{
		// if (TargetIsFiring() > 0.5)
		// {
		// 	return 1;
		// }

		return FormatConsiderationVal(
		// NegativeWeaponsChargedConsideration() *
		LegsSystemActiveConsideration() *
		TargetIsFiring()
		// NegativeTaggingConsideration() *
		// DeployedSiphonConsideration(1f, 0.7f)
		);
	}

	private float ConsiderDeploySiphonGoal()
	{
		float distConsideration;
		float targetHeatConsideration;

		// if (TargetFarFromSiphonConsideration() > 20)
		// {
		// 	distConsideration = TargetFarFromSiphonConsideration() * Target_HeatConsideration(3);
		// 	Debug.Log("Far away: " + distConsideration);
		// }
		// else
		// {
		// 	distConsideration = TargetFarFromSiphonConsideration() * Target_HeatConsideration(5) * TargetDoesNotHaveLOSToSiphon();
		// 	Debug.Log("Close: " + distConsideration);
		// }
		// distConsideration = TargetFarFromSiphonConsideration() * Target_HeatConsideration(5) * TargetDoesNotHaveLOSToSiphon();

		if (bodyState.siphonTarget == null)
		{
			return 1;
		}
		else
		{
			distConsideration = TargetFarFromSiphonConsideration();
			// Debug.Log(distConsideration);

			if (distConsideration >= 1)
			{
				targetHeatConsideration = 1;
			}
			else
			{
				targetHeatConsideration = Target_HeatConsideration(10);
			}

			return FormatConsiderationVal(
			LegsSystemActiveConsideration() *
			NegativeHeatConsideration() *
			targetHeatConsideration *
			DeployedSiphonConsideration(0f, 1.70f) *
			TargetDeployedSiphonConsideration(0, 1) *
			 distConsideration
			// CloseToSiphonConsideration()
			* SiphonAmountLeftConsideration()
			);
			//* weaponsChargedConsideration
			;
		}
	}

	private float ConsiderDefendSiphonGoal()
	{
		if (bodyState.siphon.siphonTarget == null)
		{
			return 0;
		}
		return FormatConsiderationVal(
				NegativeHeatConsideration() *
				DeployedSiphonConsideration(1.0f, 0f) *
				// CloseToSiphonConsideration() *
				TargetIsNotFiring()
				* SiphonAmountLeftConsideration()
				);
		//* weaponsChargedConsideration
		;
	}


	private float ConsiderOverheatTargetGoal()
	{
		// float heatConsideration = -(Mathf.Pow((bodyState.HeatContainer_getCurrentHeat() / bodyState.cooling.GetMaxHeat()), 3)) + 1;
		// float weaponsChargedConsideration = bodyState.Weapons_numWeaponsCharged() == 0 ? 0 : bodyState.Weapons_numWeaponsCharged() / 3;
		// float target_heatConsideration = Mathf.Clamp(Mathf.Pow((bodyState.Cooling_getCurrentHeat() / bodyState.cooling.getMaxHeat()), 2), 0.8f, 1f);



		return FormatConsiderationVal(
		 // NegativeHeatConsideration() *
		 // WeaponsChargedConsideration() *
		 // DeployedSiphonConsideration(0f, 1f) *
		 // TargetDeployedSiphonConsideration(1, 0.5f) *
		 //CloseToTargetConsideration() *
		 // TargetIsNotFiring() *
		 AwareOfHostileConsideration()
		);
		;
	}

	#region ----**** Considerations ****----

	private float HealthConsideration()
	{
		return bodyState.head.health / bodyState.head.getMaxHealth();
	}

	/// <summary>
	/// High score for low heat, low score for high heat, considering ambient temperature as the minimum possible heat
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float NegativeHeatConsideration()
	{
		float currentHeat = bodyState.HeatContainer_getCurrentHeat();
		float maxHeat = bodyState.cooling.GetMaxHeat();
		float ambientTemperature = Mathf.Clamp(bodyState.heatContainer.GetAirTemperature(), 0, maxHeat);

		/*

		// Handle the case where ambientTemperature is equal to maxHeat
		if (Mathf.Approximately(ambientTemperature, maxHeat))
		{
			// If ambient temperature is at max heat, the normalized heat should be 0 (since currentHeat cannot be less than ambientTemperature)
			return 1.0f; // Return the maximum score since the heat is at the minimum possible value (ambient temperature)
		}

		// Ensure we are not trying to cool below the ambient temperature
		float normalizedHeat = (currentHeat - ambientTemperature) / (maxHeat - ambientTemperature);

		return -(Mathf.Pow(normalizedHeat, 3)) + 1;
		
		*/

		return Mathf.Clamp(Mathf.Pow(currentHeat / maxHeat, -0.7f) - 1, 0, 1);

	}

	/// <summary>
	/// High score for high heat, low score for low heat, considering ambient temperature as the minimum possible heat
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float PositiveHeatConsideration(float threshold = 0.8f)
	{
		float currentHeat = bodyState.HeatContainer_getCurrentHeat();
		float maxHeat = bodyState.cooling.GetMaxHeat();
		float ambientTemperature = Mathf.Clamp(bodyState.heatContainer.GetAirTemperature(), 0, maxHeat);

		/*
				// Handle the case where ambientTemperature is equal to maxHeat
				if (Mathf.Approximately(ambientTemperature, maxHeat))
				{
					// If ambient temperature is at max heat, the normalized heat should be 0 (since currentHeat cannot be less than ambientTemperature)
					return 0; // Return 0 since there is no heat above ambient temperature
				}

				// Ensure we are not trying to cool below the ambient temperature
				float normalizedHeat = (currentHeat - ambientTemperature) / (maxHeat - ambientTemperature);

				if (normalizedHeat > threshold)
				{
					return Mathf.Pow(normalizedHeat + 0.3f, 3);
				}
				else
				{
					return 0;
				}
				*/

		return Mathf.Pow(currentHeat / maxHeat, threshold);

	}

	private float AirTemperatureConsideration()
	{
		return bodyState.heatContainer.GetAirTemperature() > bodyState.cooling.GetMaxHeat() ? 0 : 1;
	}

	/// <summary>
	/// High score for being tagged a lot, low score for not having much tagging
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float NegativeTaggingConsideration()
	{
		return -(Mathf.Pow((bodyState.Legs_getTaggingHealth() / 100), 2)) + 1;
	}

	/// <summary>
	/// Higher score based on number of weapons charged, 0 if no weapons charged 
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float WeaponsChargedConsideration()
	{
		return bodyState.Weapons_numWeaponsCharged() == 0 ? 0 : bodyState.Weapons_numWeaponsCharged() / 3;
	}

	/// <summary>
	/// Higher score based on less number of weapons charged, 0 if all weapons charged 
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float NegativeWeaponsChargedConsideration()
	{
		// If all weapons are charged, return 0; if none are charged, return 1
		return 1 - (bodyState.Weapons_numWeaponsCharged() / 3);
	}

	/// <summary>
	/// High score for target having high heat, low score for target having low heat
	/// </summary>
	/// <returns>float between 0 and 1</returns>
	private float Target_HeatConsideration(int exp = 8)
	{
		if (bodyState.targetBodyState != null)
		{
			//Debug.Log(Mathf.Pow((bodyState.targetBodyState.HeatContainer_getCurrentHeat() - bodyState.heatContainer.GetAirTemperature()) / (bodyState.targetBodyState.cooling.GetMaxHeat() - bodyState.heatContainer.GetAirTemperature()), 8));
			return Mathf.Pow((bodyState.targetBodyState.HeatContainer_getCurrentHeat()) / (bodyState.targetBodyState.cooling.GetMaxHeat()), exp);
		}
		else
		{
			return 1f;
		}
	}

	/// <summary>
	/// Score to determine if siphon is deployed
	/// </summary>
	/// <returns>deployedVal if siphon is deployed, notDeployedVal if siphon not deployed</returns>
	private float DeployedSiphonConsideration(float deployedVal, float notDeployedVal)
	{
		return bodyState.Siphon_isExtended() ? deployedVal : notDeployedVal;
	}

	private float TargetDeployedSiphonConsideration(float deployedVal, float notDeployedVal)
	{
		if (bodyState.targetBodyState != null)
		{
			return bodyState.targetBodyState.Siphon_isExtended() ? deployedVal : notDeployedVal;
		}
		else
		{
			return 1f;
		}
	}

	/// <summary>
	/// Determine if there is anything left to siphon 
	/// </summary>
	/// <returns>1 if there is still siphon left OR we haven't registered any siphon targets yet, 0 if we registered a siphon target and it has no dollars left</returns>
	private float SiphonAmountLeftConsideration()
	{
		return bodyState.siphonTarget.dollarsLeft > 1 ? 1 : 0;
	}

	/// <summary>
	/// Score to determine if we have seen a hostile yet
	/// </summary>
	/// <returns>low value if we are not aware of a hostile, 1 if we are aware</returns>
	private float AwareOfHostileConsideration()
	{
		if (bodyState.targetBodyState != null)
		{
			return 1f;
		}
		else
		{
			return 0.2f;
		}
	}

	private float CloseToTargetConsideration()
	{
		if (bodyState.targetBodyState != null)
		{
			return 10 / Vector3.Distance(bodyState.targetBodyState.gameObject.transform.position, bodyState.gameObject.transform.position);
		}
		else
		{
			return 1f;
		}
	}

	private float FarFromTargetConsideration()
	{
		if (bodyState.targetBodyState != null)
		{
			return Vector3.Distance(bodyState.targetBodyState.gameObject.transform.position, bodyState.gameObject.transform.position) / 10;
		}
		else
		{
			return 1f;
		}
	}

	private float TargetFarFromSiphonConsideration()
	{
		if (bodyState.targetBodyState != null && bodyState.siphonTarget != null)
		{
			return FormatConsiderationVal(Vector3.Distance(bodyState.targetBodyState.gameObject.transform.position, bodyState.siphonTarget.gameObject.transform.position) / bodyState.siphon.getMaxSiphonDistance());
		}
		else
		{
			return 1f;
		}
	}

	private float CloseToSiphonConsideration()
	{
		if (bodyState.siphon.siphonTarget != null)
		{
			return FormatConsiderationVal(10 / Vector3.Distance(bodyState.gameObject.transform.position, bodyState.siphonTarget.gameObject.transform.position) * 2);
		}
		else
		{
			return 1f;
		}
	}

	private float TargetDoesNotHaveLOSToSiphon()
	{
		if (bodyState.targetBodyState == null)
		{
			return 1f;
		}
		if (bodyState.targetBodyState.Siphon_haveLOS())
		{
			return 0.5f;
		}
		else
		{
			return 1.5f;
		}
	}

	private float TargetIsFiring()
	{
		if (bodyState.targetBodyState == null)
		{
			return 0f;
		}
		if (bodyState.targetBodyState.Weapons_currentlyFiring() || bodyState.targetBodyState.Weapons_currentlyFiringBurst())
		{
			return 1f;
		}
		else
		{
			return 0.0f;
		}
	}

	private float TargetIsNotFiring()
	{
		if (bodyState.targetBodyState == null)
		{
			return 1f;
		}
		if (bodyState.targetBodyState.Weapons_currentlyFiring() || bodyState.targetBodyState.Weapons_currentlyFiringBurst())
		{
			return 0.1f;
		}
		else
		{
			return 1f;
		}
	}

	private int LegsSystemActiveConsideration()
	{
		return bodyState.legs.getMoveSpeed() > 0 ? 1 : 0;
	}

	#endregion

	void DebugLogGoal(GoalBase goal)
	{
		switch (goal)
		{
			case OverheatHostileGoal:
				Debug.Log("Attacking");
				break;
			case TakeCoverGoal:
				Debug.Log("Taking Cover");
				break;
			case DeploySiphonGoal:
				Debug.Log("Deploying Siphon");
				break;
			case DefendSiphonGoal:
				Debug.Log("Defending Siphon");
				break;
			case CooldownGoal:
				Debug.Log("Cooling Down");
				break;
			default:
				Debug.Log("Defaulting");
				break;
		}
	}
}