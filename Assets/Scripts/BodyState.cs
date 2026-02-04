using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class BodyState : MonoBehaviour
{
	BodyController bodyController;
	public HeatContainer heatContainer;
	public HeadModel head;
	public CoolingModel cooling;
	public LegsModel legs;
	private SensorsModel sensors;
	public WeaponsModel weapons;
	public SiphonModel siphon;
	public Rigidbody rb;

	public float bodyHeat;
	public bool bodyIsOverheated;
	public bool isDead;

	public AttackConfigSO AttackConfig;
	public float dangerLevel;
	private float losCheckInterval = 0.2f;
	private float losCheckIntervalCache = 0.2f;
	public bool hasLOS;
	public bool isBeingAimedAt;
	public float TimeToAim;
	public bool isAimed = false;
	public float hitStunAmount;

	public Collider headCollider;

	public Collider rightArm;
	public Collider rightLeg;

	public Collider leftLeg;

	public GameObject positionTracker;
	public GameObject positionTracker2;


	public BodyState targetBodyState;

	public SiphonTarget siphonTarget;

	public LayerMask ObstructionLayerMask;

	public LayerMask AttackableLayerMask;


	public void Init(List<SystemModel> systems, HeatContainer heat, BodyController bc)
	{
		bodyController = bc;
		heatContainer = heat;

		cooling = systems.OfType<CoolingModel>().FirstOrDefault();
		head = systems.OfType<HeadModel>().FirstOrDefault();
		legs = systems.OfType<LegsModel>().FirstOrDefault();
		sensors = systems.OfType<SensorsModel>().FirstOrDefault();
		weapons = systems.OfType<WeaponsModel>().FirstOrDefault();
		siphon = systems.OfType<SiphonModel>().FirstOrDefault();

		if (bc.isAI)
		{
			AttackConfig = GetComponentInParent<GoapSetBinder>().GoapRunner.GetComponent<DependencyInjector>().AttackConfig;
		}
	}

	void Update()
	{

		if (bodyController.isAI) UpdateAIState();

		if (losCheckIntervalCache > 0)
		{
			losCheckIntervalCache -= Time.deltaTime;
		}
		else
		{
			hasLOS = Target_HaveLOS();
			losCheckIntervalCache = losCheckInterval;
		}
		// bodyHeat = heatContainer.currentTemperature;
		// bodyIsOverheated = cooling.isOverheated;
	}

	void UpdateAIState()
	{
		if (rb.velocity.magnitude > 0.0001f)
		{
			TimeToAim = Mathf.Clamp(TimeToAim += Time.deltaTime * 3, 0, AttackConfig.TimeToAim);
			isAimed = false;
		}
	}

	public int Cooling_getSystemHealth()
	{
		return cooling.currentLevel;
	}

	public float HeatContainer_getCurrentHeat()
	{
		return heatContainer.currentTemperature;
	}

	public bool Cooling_IsOverheated()
	{
		return cooling.isOverheated;
	}

	public int Legs_getSystemHealth()
	{
		return legs.currentLevel;
	}

	public float Legs_getTaggingHealth()
	{
		return legs.taggingModifier;
	}

	public int Weapons_getSystemHealth()
	{
		return weapons.currentLevel;
	}

	public bool Weapons_weapon1Powered()
	{
		return weapons.GetCurrentPowerAllocationDictionary()[0];
	}

	public bool Weapons_weapon1Charged()
	{
		return weapons.guns[0].isCharged();
	}

	public bool Weapons_weapon2Powered()
	{
		return weapons.GetCurrentPowerAllocationDictionary()[1];
	}
	public bool Weapons_weapon2Charged()
	{
		return weapons.guns[1].isCharged();
	}

	public bool Weapons_weapon3Powered()
	{
		return weapons.GetCurrentPowerAllocationDictionary()[2];
	}
	public bool Weapons_weapon3Charged()
	{
		return weapons.guns[2].isCharged();
	}

	public bool Weapons_noWeaponsCharged()
	{
		return !(Weapons_weapon1Charged() || Weapons_weapon2Charged() || Weapons_weapon3Charged());
	}

	public int Weapons_numWeaponsCharged()
	{
		int n = 0;
		if (weapons.guns[0].isCharged())
		{
			n++;
		}

		if (weapons.guns[1].isCharged())
		{
			n++;
		}

		if (weapons.guns[2].isCharged())
		{
			n++;
		}

		return n;
	}

	public bool[] Weapons_currentWeaponsCharged()
	{
		return new bool[] { weapons.guns[0].isCharged(), weapons.guns[1].isCharged(), weapons.guns[2].isCharged() };
	}

	public bool[] Weapons_currentWeaponsPowered()
	{
		return weapons.GetCurrentPowerAllocationDictionary();
	}

	public bool Weapons_currentlyFiringBurst()
	{
		return weapons.guns[0].isFiringBurst || weapons.guns[1].isFiringBurst || weapons.guns[2].isFiringBurst;
	}

	public bool Weapons_currentlyFiring()
	{
		return weapons.guns[0].isFiring || weapons.guns[1].isFiring || weapons.guns[2].isFiring;
	}

	public int Sensors_getSystemHealth()
	{
		return sensors.currentLevel;
	}

	public int Siphon_getSystemHealth()
	{
		return siphon.currentLevel;
	}

	public bool Siphon_isExtended()
	{
		return siphon.extended;
	}

	public bool Siphon_haveLOS()
	{
		bool haveLOS = false;
		if (siphonTarget == null)
		{
			return haveLOS;
		}
		Vector3 direction1 = (siphonTarget.transform.position - headCollider.transform.position).normalized;
		RaycastHit hit1;

		if (Physics.SphereCast(headCollider.transform.position, 0.02f, direction1, out hit1, Mathf.Infinity, siphon.siphonLayerMask | ObstructionLayerMask))
		{
			//Debug.Log(agent.transform.position);
			haveLOS = hit1.transform.GetComponent<SiphonTarget>() != null;
		}
		return haveLOS;
	}

	public bool Target_HaveLOS()
	{
		bool haveLOS = false;
		if (targetBodyState == null)
		{
			return haveLOS;
		}
		Vector3 direction1 = (targetBodyState.transform.position - headCollider.transform.position).normalized;
		RaycastHit hit1;

		if (Physics.SphereCast(headCollider.transform.position, 0.25f, direction1, out hit1, Mathf.Infinity, AttackableLayerMask | ObstructionLayerMask))
		{
			//Debug.Log(agent.transform.position);
			haveLOS = hit1.transform.GetComponent<PlayerController>() != null;
		}
		return haveLOS;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (other.gameObject.layer == 13 && Target_HaveLOS())
		{
			isBeingAimedAt = true;
		}
	}

	// private void OnTriggerStay(Collider other)
	// {
	// 	if (other.gameObject.layer == 13)
	// 	{
	// 		isBeingAimedAt = true;
	// 	}
	// }

	private void OnTriggerExit(Collider other)
	{
		if (other.gameObject.layer == 13)
		{
			isBeingAimedAt = false;
		}
	}

	#region AI data
	public Gun desiredGunToUse;
	#endregion
}