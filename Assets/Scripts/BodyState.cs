using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.AI;

public enum EnemyFireReadinessState
{
	Unset,
	Set
}

public class BodyState : MonoBehaviour
{
	public BodyController bodyController;
	public HeatContainer heatContainer;
	public HeadModel head;
	public CoolingModel cooling;
	public LegsModel legs;
	private SensorsModel sensors;
	public WeaponsModel weapons;
	public SiphonModel siphon;
	public Rigidbody rb;
	private NavMeshAgent navMeshAgent;

	public float bodyHeat;
	public bool bodyIsOverheated;
	public bool isDead;

	public AttackConfigSO AttackConfig;
	public float dangerLevel;
	private float losCheckInterval = 0.2f;
	private float losCheckIntervalCache = 0.2f;
	public bool hasLOS;
	public bool isBeingAimedAt;
	private Dictionary<Gun, int> suppressiveAimGunOverlapCounts = new Dictionary<Gun, int>();
	public float TimeToAim;
	public bool isAimed = false;
	public float hitStunAmount;
	[Min(0f)] public float hitStunDecayDelay = 0.1f;
	private float hitStunDecayDelayTimer;
	public EnemyFireReadinessState FireReadinessState = EnemyFireReadinessState.Unset;
	private bool fireReadinessInitialized;

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

	[SerializeField] private float AIMovingVelocityThreshold = 0.05f;
	[SerializeField] private float AIMovingRootVelocityThreshold = 0.05f;
	private Transform AIMovingRoot;
	private Vector3 AIMovingLastRootPosition;
	private bool AIMovingHasLastRootPosition;
	private int AIMovingCachedFrame = -1;
	private bool AIMovingCachedValue;


	public void Init(List<SystemModel> systems, HeatContainer heat, BodyController bc)
	{
		bodyController = bc;
		heatContainer = heat;
		navMeshAgent = GetComponentInParent<NavMeshAgent>();

		cooling = systems.OfType<CoolingModel>().FirstOrDefault();
		head = systems.OfType<HeadModel>().FirstOrDefault();
		legs = systems.OfType<LegsModel>().FirstOrDefault();
		sensors = systems.OfType<SensorsModel>().FirstOrDefault();
		weapons = systems.OfType<WeaponsModel>().FirstOrDefault();
		siphon = systems.OfType<SiphonModel>().FirstOrDefault();

		if (bc.isAI)
		{
			AttackConfig = GetComponentInParent<GoapSetBinder>().GoapRunner.GetComponent<DependencyInjector>().AttackConfig;
			InitializeFireReadiness();
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

	private void OnDisable()
	{
		ClearSuppressiveAimGunOverlaps();
	}

	void UpdateAIState()
	{
		if (FireReadinessState == EnemyFireReadinessState.Set)
		{
			TimeToAim = 0f;
			isAimed = true;
		}
	}

	public bool TickFireReadiness(float deltaTime, bool hasKnownTarget)
	{
		EnsureFireReadinessInitialized();

		if (FireReadinessState == EnemyFireReadinessState.Set)
		{
			TimeToAim = 0f;
			isAimed = true;
			return true;
		}

		isAimed = false;
		if (!hasKnownTarget)
		{
			return false;
		}

		TimeToAim = Mathf.Max(0f, TimeToAim - deltaTime);
		if (TimeToAim > 0f)
		{
			return false;
		}

		EnterSetFireReadiness();
		return true;
	}

	public void NotifyShotByPlayer()
	{
		if (bodyController == null || !bodyController.isAI)
		{
			return;
		}

		EnsureFireReadinessInitialized();
		if (FireReadinessState == EnemyFireReadinessState.Set)
		{
			float hitStunToUnset = AttackConfig != null ? Mathf.Max(0f, AttackConfig.HitStunToUnsetFireReadiness) : 0.9f;
			if (hitStunAmount <= hitStunToUnset)
			{
				return;
			}
		}

		EnterUnsetFireReadiness();
	}

	public void RestartHitStunDecayDelay()
	{
		hitStunDecayDelayTimer = Mathf.Max(0f, hitStunDecayDelay);
	}

	public bool TickHitStunDecayDelay(float deltaTime)
	{
		if (hitStunDecayDelayTimer <= 0f)
		{
			return false;
		}

		hitStunDecayDelayTimer = Mathf.Max(0f, hitStunDecayDelayTimer - deltaTime);
		return true;
	}

	private void InitializeFireReadiness()
	{
		fireReadinessInitialized = true;
		EnterUnsetFireReadiness();
	}

	private void EnsureFireReadinessInitialized()
	{
		if (fireReadinessInitialized || bodyController == null || !bodyController.isAI)
		{
			return;
		}

		InitializeFireReadiness();
	}

	private void EnterUnsetFireReadiness()
	{
		FireReadinessState = EnemyFireReadinessState.Unset;
		TimeToAim = AttackConfig != null ? Mathf.Max(0f, AttackConfig.TimeToAim) : 0f;
		isAimed = false;
	}

	private void EnterSetFireReadiness()
	{
		FireReadinessState = EnemyFireReadinessState.Set;
		TimeToAim = 0f;
		isAimed = true;
	}

	public bool IsAIMoving()
	{
		if (AIMovingCachedFrame == Time.frameCount)
		{
			return AIMovingCachedValue;
		}

		AIMovingCachedFrame = Time.frameCount;
		AIMovingCachedValue = CalculateIsAIMoving();
		return AIMovingCachedValue;
	}

	private bool CalculateIsAIMoving()
	{
		float moveThresholdSqr = AIMovingVelocityThreshold * AIMovingVelocityThreshold;
		if (rb != null && rb.velocity.sqrMagnitude > moveThresholdSqr)
		{
			UpdateAIMovingRootPosition();
			return true;
		}

		return IsAIMovingRootDisplaced();
	}

	private bool IsAIMovingRootDisplaced()
	{
		if (bodyController == null || !bodyController.isAI)
		{
			return false;
		}

		if (AIMovingRoot == null)
		{
			AIMovingRoot = transform.parent;
		}

		if (AIMovingRoot == null)
		{
			return false;
		}

		Vector3 currentRootPosition = AIMovingRoot.position;
		if (!AIMovingHasLastRootPosition)
		{
			AIMovingLastRootPosition = currentRootPosition;
			AIMovingHasLastRootPosition = true;
			return false;
		}

		float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
		Vector3 displacement = currentRootPosition - AIMovingLastRootPosition;
		displacement.y = 0f;
		AIMovingLastRootPosition = currentRootPosition;

		float rootVelocityThresholdSqr = AIMovingRootVelocityThreshold * AIMovingRootVelocityThreshold;
		return displacement.sqrMagnitude / (deltaTime * deltaTime) > rootVelocityThresholdSqr;
	}

	private void UpdateAIMovingRootPosition()
	{
		if (bodyController == null || !bodyController.isAI)
		{
			return;
		}

		if (AIMovingRoot == null)
		{
			AIMovingRoot = transform.parent;
		}

		if (AIMovingRoot == null)
		{
			return;
		}

		AIMovingLastRootPosition = AIMovingRoot.position;
		AIMovingHasLastRootPosition = true;
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
		bool resolvedAimGun = TryResolvePlayerAimGun(other, out Gun aimGun);

		if (resolvedAimGun)
		{
			AddSuppressiveAimGunOverlap(aimGun);
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
		if (TryResolvePlayerAimGun(other, out Gun aimGun))
		{
			RemoveSuppressiveAimGunOverlap(aimGun);
		}
	}

	private bool TryResolvePlayerAimGun(Collider other, out Gun aimGun)
	{
		aimGun = null;
		if (other == null || other.gameObject.layer != 13)
		{
			return false;
		}

		BodyController aimOwner = other.GetComponentInParent<BodyController>();
		if (aimOwner == null || aimOwner.isAI || aimOwner.GetComponentInParent<PlayerController>() == null)
		{
			return false;
		}

		GunAimOwner gunAimOwner = other.GetComponentInParent<GunAimOwner>();
		if (gunAimOwner == null || gunAimOwner.Gun == null)
		{
			return false;
		}

		aimGun = gunAimOwner.Gun;
		return true;
	}

	private void AddSuppressiveAimGunOverlap(Gun aimGun)
	{
		if (aimGun == null)
		{
			return;
		}

		if (suppressiveAimGunOverlapCounts.TryGetValue(aimGun, out int overlapCount))
		{
			suppressiveAimGunOverlapCounts[aimGun] = overlapCount + 1;
		}
		else
		{
			suppressiveAimGunOverlapCounts.Add(aimGun, 1);
			aimGun.ActualShotFired += OnSuppressiveAimGunActualShotFired;
		}

		isBeingAimedAt = suppressiveAimGunOverlapCounts.Count > 0;
	}

	private void RemoveSuppressiveAimGunOverlap(Gun aimGun)
	{
		if (aimGun == null || !suppressiveAimGunOverlapCounts.TryGetValue(aimGun, out int overlapCount))
		{
			return;
		}

		overlapCount--;
		if (overlapCount > 0)
		{
			suppressiveAimGunOverlapCounts[aimGun] = overlapCount;
		}
		else
		{
			suppressiveAimGunOverlapCounts.Remove(aimGun);
			aimGun.ActualShotFired -= OnSuppressiveAimGunActualShotFired;
		}

		isBeingAimedAt = suppressiveAimGunOverlapCounts.Count > 0;
	}

	private void ClearSuppressiveAimGunOverlaps()
	{
		foreach (Gun aimGun in suppressiveAimGunOverlapCounts.Keys)
		{
			if (aimGun != null)
			{
				aimGun.ActualShotFired -= OnSuppressiveAimGunActualShotFired;
			}
		}

		suppressiveAimGunOverlapCounts.Clear();
		isBeingAimedAt = false;
	}

	private void OnSuppressiveAimGunActualShotFired(Gun aimGun)
	{
		if (aimGun == null || !suppressiveAimGunOverlapCounts.ContainsKey(aimGun))
		{
			return;
		}

		ApplySuppressiveShot();
	}

	private void ApplySuppressiveShot()
	{
		if (bodyController == null || !bodyController.isAI)
		{
			return;
		}

		EnsureFireReadinessInitialized();
		if (FireReadinessState != EnemyFireReadinessState.Unset || AttackConfig == null)
		{
			return;
		}

		TimeToAim = Mathf.Clamp(TimeToAim + AttackConfig.SuppressiveShotTimeToAimIncrease, 0f, AttackConfig.TimeToAim);
		isAimed = false;
	}

	#region AI data
	public Gun desiredGunToUse;
	#endregion
}
