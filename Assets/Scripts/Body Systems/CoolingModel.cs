using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using static Lean.Pool.LeanGameObjectPool;

public class CoolingModel : SystemModel
{
	// public Rigidbody rb;
	// public float currentHeat = 0;
	// float maxHeatMultiplier = 10f;

	// public bool isOverheated = false;
	// public bool delayElapsed = false;
	// private float coolingStartDelay = 0.1f;
	// private float coolingStartDelayTemp = 0;
	// private float coolingAmountMultiplier = 0.2f;
	// private float coolingAmountMultiplierPassive = 0.1f;
	// private float coolingAmountMultiplierOverheated = 3f;


	public delegate void CoolingEventHandler();
	public event CoolingEventHandler RaiseIncreasedHeat;
	public event CoolingEventHandler RaiseOverheated;
	public event CoolingEventHandler RaiseCooledDownFromOverheat;

	// public CoolingModel(int currentLvl, Rigidbody r) : base(currentLvl)
	// {
	//     rb = r;
	// }

	// public override void SetNameAndMaxLevel()
	// {
	//     name = BodyInfo.systemID.Cooling;
	//     maxLevel = 12;
	// }

	protected override void InitCommands()
	{
	}

	// public float getMaxHeat()
	// {
	//     return currentLevelWithoutDamage * maxHeatMultiplier;
	// }

	// public float getCoolingAmount()
	// {
	//     return currentLevel * coolingAmountMultiplier;
	// }

	// public float getPassiveCoolingAmount()
	// {
	//     return currentLevelWithoutDamage * coolingAmountMultiplierPassive;
	// }

	// public void IncreaseHeat(object e, float amount)
	// {
	//     ResetCooldown();
	//     if (!isOverheated)
	//     {
	//         currentHeat = Mathf.Clamp(currentHeat += amount, 0, getMaxHeat());
	//         if (currentHeat >= getMaxHeat())
	//         {
	//             Debug.Log("Overheated!");
	//             isOverheated = true;
	//             RaiseOverheated?.Invoke();
	//         }
	//     }
	//     RaiseIncreasedHeat?.Invoke();
	// }

	// private void DecreaseCoolingStartDelay()
	// {
	//     if (coolingStartDelayTemp > 0)
	//     {
	//         coolingStartDelayTemp -= Time.deltaTime;
	//     }
	//     else
	//     {
	//         delayElapsed = true;
	//     }
	// }

	// public void PassiveCooldown()
	// {
	//     if (currentHeat > 0)
	//     {
	//         currentHeat = Mathf.Clamp(currentHeat -= getPassiveCoolingAmount() * Time.deltaTime, 0, getMaxHeat());
	//     }
	//     if (currentHeat <= 0 && isOverheated == true)
	//     {
	//         RaiseCooledDownFromOverheat?.Invoke();
	//         isOverheated = false;
	//     }
	// }

	// public void Cooldown()
	// {
	//     if (delayElapsed)
	//     {
	//         if (currentHeat > 0)
	//         {
	//             currentHeat = Mathf.Clamp(currentHeat -= getCoolingAmount() * Time.deltaTime, 0, getMaxHeat());
	//             //Debug.Log("Normal Cooldown");
	//         }
	//         else
	//         {
	//             RaiseCooledDownFromOverheat?.Invoke();
	//             isOverheated = false;
	//         }
	//     }
	//     else
	//     {
	//         DecreaseCoolingStartDelay();
	//     }
	// }

	// public void CooldownOverheated()
	// {
	//     if (currentHeat > 0)
	//     {
	//         currentHeat = Mathf.Clamp(currentHeat -= (coolingAmountMultiplierOverheated * currentLevelWithoutDamage) * Time.deltaTime, 0, getMaxHeat());
	//         //Debug.Log("Overheat Cooldown");
	//     }
	//     else
	//     {
	//         RaiseCooledDownFromOverheat?.Invoke();
	//         isOverheated = false;
	//     }
	// }

	// public void ResetCooldown()
	// {
	//     delayElapsed = false;
	//     coolingStartDelayTemp = coolingStartDelay;
	// }

	//public IEnumerator DecreaseHeatCoroutine()
	//{
	//    yield return new WaitForSeconds(coolingStartDelay);

	//    while (currentHeat > 0)
	//    {
	//        currentHeat -= getCoolingAmount() * Time.deltaTime;
	//        yield return null;
	//    }

	//    // Optional: Do something when heat reaches zero
	//    //Debug.Log("Heat has reached zero!");
	//    //isCoolingDown = false;
	//    RaiseCooledDownFromOverheat?.Invoke();
	//}

	public Rigidbody rb;
	public bool isOverheated = false; // Track whether the mech is overheated
	public bool isStandingStill = false; // To track if the mech is standing still

	public float minimumTemperature = 21f;

	private float maxHeatMultiplier = 260f;
	private float passiveCoolingMultiplier = 0.0f;
	private float coolingMultiplier = 300f;
	private float overheatingCoolingMultiplier = 70000f;

	public enum CoolingState { PassiveCooldown, Cooldown, CooldownOverheated }
	public CoolingState currentCoolingState;

	public event Action OnCoolingStateChanged; // Notify HeatContainer of state changes

	public CoolingModel(int currentLvl, Rigidbody r) : base(currentLvl)
	{
		rb = r;
	}

	public override void SetNameAndMaxLevel()
	{
		name = BodyInfo.systemID.Cooling;
		maxLevel = 12;
	}

	// Max heat capacity determined by the mech's system level
	public float GetMaxHeat()
	{
		return currentLevelWithoutDamage * maxHeatMultiplier;
	}

	// Passive cooling rate (always happens)
	public float GetPassiveCoolingMultiplier()
	{
		return 0;
	}

	// Regular cooling (mech is standing still)
	public float GetCooldownMultiplier()
	{
		return 1 + currentLevel * coolingMultiplier;
	}

	// Overheated cooling rate (when the mech is overheated)
	public float GetOverheatedCoolingMultiplier()
	{
		return currentLevelWithoutDamage * overheatingCoolingMultiplier;
	}

	// Method to update the cooling state based on conditions
	public void UpdateCoolingState()
	{
		if (isOverheated)
		{
			SetCoolingState(CoolingState.CooldownOverheated);
		}
		else if (isStandingStill)
		{
			SetCoolingState(CoolingState.Cooldown);
		}
		else
		{
			SetCoolingState(CoolingState.PassiveCooldown);
		}
	}

	// Set cooling state and notify listeners (HeatContainer)
	private void SetCoolingState(CoolingState newState)
	{
		if (currentCoolingState != newState)
		{
			currentCoolingState = newState;
			OnCoolingStateChanged?.Invoke();
			//Debug.Log("new cooling state: " + newState);
		}
	}

	// Call this method when the mech overheats
	public void SetOverheated(bool overheated)
	{
		if (overheated)
		{
			//Debug.Log("CoolingModel: Mech is overheated!");
			RaiseOverheated?.Invoke();
			isOverheated = true;
			UpdateCoolingState();
		}
		else if (!overheated)
		{
			//Debug.Log("CoolingModel: Mech is not overheated");
			RaiseCooledDownFromOverheat?.Invoke();
			isOverheated = false;
			UpdateCoolingState();
		}
	}

	// Call this when the mech starts/stops moving
	public void SetStandingStill(bool standingStill)
	{
		isStandingStill = standingStill;
		UpdateCoolingState();
	}

	// Handle state transitions when heat reaches 0 or max
	public void HandleHeatExtremes(float currentHeat, float maxHeat)
	{
		if (currentHeat >= maxHeat && !isOverheated)
		{
			RaiseOverheated?.Invoke();
			SetOverheated(true);
		}
		else if (currentHeat <= minimumTemperature && isOverheated)
		{
			RaiseCooledDownFromOverheat?.Invoke();
			SetOverheated(false);
		}
	}

}
