using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

public class WeaponsModel : SystemModel
{
	public GunSelector gunSelector;
	public GunSelector gunSelectorL;

	public Rigidbody weaponRb;
	public Gun[] guns;
	public Gun[] gunsL;


	private int currentAllocationIndex;
	public bool[][] currentAllocations;

	bool canFire = true;

	public float disruptionModifier = 100f;

	public float disruptionRecoveryRate = 0.2f;

	public float disruptionRecoveryRateCache = 0.2f;

	public float disruptionRecoveryRateRecoveryRate = 0.0001f;

	public delegate void WeaponsEventHandler(object sender, float h);
	public event WeaponsEventHandler RaiseFiredWeapon;

	public WeaponsModel(int currentLvl, GunSelector g, GunSelector gL, Rigidbody rb) : base(currentLvl)
	{
		//Debug.Log(g.ActiveGun1.gunData.GunName);

		gunSelector = g;
		gunSelectorL = gL;
		weaponRb = rb;
		guns = new Gun[3];
		guns[0] = gunSelector.ActiveGun1;
		guns[1] = gunSelector.ActiveGun2;
		guns[2] = gunSelector.ActiveGun3;

		gunsL = new Gun[3];
		gunsL[0] = gunSelectorL.ActiveGun1;
		gunsL[1] = gunSelectorL.ActiveGun2;
		gunsL[2] = gunSelectorL.ActiveGun3;

		DetermineValidAllocations();
	}

	public override void SetNameAndMaxLevel()
	{
		name = BodyInfo.systemID.Weapons;
		maxLevel = 12;
	}

	protected override void InitCommands()
	{
	}

	private void DetermineValidAllocations()
	{
		Dictionary<string, bool[]> allocations = new Dictionary<string, bool[]>();

		//PPP
		if (GetTotalPowerOfGuns() <= currentLevel)
		{
			allocations.Add("PPP", new bool[] { true, true, true });
		}
		else
		{
			//PUU
			if (guns[0].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("PUU", new bool[] { true, false, false });
			}
			//UUP
			if (guns[2].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("UUP", new bool[] { false, false, true });
			}
			//UPU
			if (guns[1].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("UPU", new bool[] { false, true, false });
			}
			//UPP
			if (guns[1].gunData.shootConfig.powerRequired + guns[2].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("UPP", new bool[] { false, true, true });
				allocations.Remove("UPU");
				allocations.Remove("UUU");
			}
			//PPU
			if (guns[0].gunData.shootConfig.powerRequired + guns[1].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("PPU", new bool[] { true, true, false });
				allocations.Remove("UPU");
				allocations.Remove("PUU");
			}
			//PUP
			if (guns[0].gunData.shootConfig.powerRequired + guns[2].gunData.shootConfig.powerRequired <= currentLevel)
			{
				allocations.Add("PUP", new bool[] { true, false, true });
				allocations.Remove("PUU");
				allocations.Remove("UUP");
			}
			//UUU
			if (allocations.Count == 0)
			{
				allocations.Add("UUU", new bool[] { false, false, false });
			}
		}

		currentAllocations = new bool[allocations.Count][];
		allocations.Values.CopyTo(currentAllocations, 0);
	}

	private int GetTotalPowerOfGuns()
	{
		int gun1power = guns[0] == null ? 0 : guns[0].gunData.shootConfig.powerRequired;
		int gun2power = guns[0] == null ? 0 : guns[1].gunData.shootConfig.powerRequired;
		int gun3power = guns[0] == null ? 0 : guns[2].gunData.shootConfig.powerRequired;

		return gun1power +
			gun2power +
			gun3power;
	}

	public void CycleToNextPowerAllocationDictionary()
	{
		if (currentAllocations.Length > 0)
		{
			currentAllocationIndex = (currentAllocationIndex + 1) % currentAllocations.Length;
		}
		else
		{
			currentAllocationIndex = 0;
		}
		guns[0].isPowered = GetCurrentPowerAllocationDictionary()[0];
		guns[1].isPowered = GetCurrentPowerAllocationDictionary()[1];
		guns[2].isPowered = GetCurrentPowerAllocationDictionary()[2];
	}

	public bool[] GetCurrentPowerAllocationDictionary()
	{
		return currentAllocations[currentAllocationIndex];
	}

	public void OnCoolingSystemOverheat()
	{
		//Debug.Log("weapons: overheated!");
		canFire = true;
	}

	public void OnCoolingSystemCooledOff()
	{
		canFire = true;
	}

	public void RecoverFromDisruption()
	{
		if (disruptionModifier < 100f)
		{
			disruptionModifier += disruptionRecoveryRateCache;
		}
		if (disruptionRecoveryRateCache < disruptionRecoveryRate)
		{
			disruptionRecoveryRateCache += disruptionRecoveryRateRecoveryRate;
		}
	}

	public void HandleDisruption(Limb l)
	{
		switch (l.specificLimb)
		{
			case Limb.LimbID.rightArm:
				DealDisruption(30f, 0.05f);
				break;
			case Limb.LimbID.head:
				DealDisruption(10f, 0.1f);
				break;
			default:
				break;
		}
	}

	private void DealDisruption(float disruptionAmount, float disruptionRecoveryAmount)
	{
		disruptionModifier = Mathf.Max(10f, disruptionModifier - disruptionAmount);
		disruptionRecoveryRateCache = Mathf.Max(0.02f, disruptionRecoveryRateCache - disruptionRecoveryAmount);
	}

	public void ExecuteWeapon1(bool arm)
	{
		GunSelector g = arm ? gunSelector : gunSelectorL;
		if (g.ActiveGun1.Shoot())
		{
			RaiseFiredWeapon?.Invoke(this, 0);
			//Debug.Log("Fire 1");
		}
	}

	public void ExecuteWeapon2()
	{
		if (canFire && GetCurrentPowerAllocationDictionary()[1])
		{
			if (gunSelector.ActiveGun2.Shoot())
			{
				RaiseFiredWeapon?.Invoke(this, 0);
				//Debug.Log("Fire 2");
			}
		}
	}

	public void ExecuteWeapon3()
	{
		if (canFire && GetCurrentPowerAllocationDictionary()[2])
		{
			if (gunSelector.ActiveGun3.Shoot())
			{
				RaiseFiredWeapon?.Invoke(this, 0);
				//Debug.Log("Fire 3");
			}
		}
	}
}
