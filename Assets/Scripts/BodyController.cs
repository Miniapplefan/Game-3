using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations.Rigging;
using static BodyInfo;
using static Limb;

public class BodyController : MonoBehaviour
{
	public BodyInfo so_initialBodyStats;

	public BodyState bodyState;

	public InputController input;

	public bool isAI = false;

	public float aiHealth;

	public bool isDead = false;

	public bool isGodMode = false;

	[Header("Movement Aim Rotate")]
	public float moveAimYawDuration = 0.15f;
	public AnimationCurve moveAimYawCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	public float moveAimYawCompleteAngle = 0.5f;
	private bool hasPendingMoveAimYaw = false;
	private Quaternion pendingMoveAimYaw;
	private Quaternion pendingMoveAimYawStart;
	private float pendingMoveAimYawElapsed = 0f;
	private bool freezeHeadDuringMoveAimYaw = false;
	private bool moveAimYawSourceIsLeft = false;
	private bool moveAimYawSourceWasRight = false;
	private bool pendingMoveAimToggleOff = false;
	private Quaternion frozenHeadRotation;
	private Quaternion frozenHeadLRotation;
	private bool hasFrozenCameraRotation = false;
	private Quaternion frozenCameraRotation;

	public bool IsMoveAimYawInProgress => freezeHeadDuringMoveAimYaw;
	public bool MoveAimYawSourceIsLeft => moveAimYawSourceIsLeft;
	public bool HasFrozenCameraRotation => hasFrozenCameraRotation;
	public Quaternion FrozenCameraRotation => frozenCameraRotation;

	// [HideInInspector]
	//public CoolingModel cooling;

	[HideInInspector]
	public HeatContainer heatContainer;
	private Coroutine decrementCoroutine = null;
	// public GameObject coolingGauge;
	// Vector3 coolingGaugeScaleCache;

	public GameObject taggingGauge;
	Vector3 taggingGaugeScaleCache;
	// public TMP_Text dollarsIndicator;
	// public TMP_Text healthIndicator;

	public HeadModel head;
	public AuraManager auraManager;

	public LegsModel legs;
	SensorsModel sensors;
	WeaponsModel weapons;
	public Rigidbody weaponRigidbody;
	public GunSelector guns;
	public GunSelector gunsL;
	public GameObject weapon1gauge;
	public GameObject weapon2gauge;
	public GameObject weapon3gauge;
	//public GunSelectorTest gun1;
	//public GunSelectorTest gun2;
	//public GunSelectorTest gun3;
	public SiphonModel siphon;
	public Transform siphonHead;

	public Transform siphonArm;

	List<SystemModel> systemControllers;
	public Rigidbody rb;
	public Rigidbody ragdollCore;
	public Rigidbody upperTorsoRb;
	public GameObject physicalHead;
	public GameObject headObject;
	public GameObject headObjectL;

	public Transform headObjectTransformCache;
	public Transform headObjectAimOffset;
	public Transform headObjectAimOffsetL;

	public GameObject aimCam;
	public bool isAimingRight = false;
	bool startedAimingRight = false;
	public bool isAimingLeft = false;
	bool startedAimingLeft = false;
	public Transform weaponAimPoint;
	public Transform weaponAimPointL;
	float leanSpeed = 0.04f;
	float leanRecoverySpeed = 0.05f;

	bool isLeaningLeft = false;
	bool isLeaningRight = false;
	bool startedLeaningLeft = false;
	bool startedLeaningRight = false;
	public MultiAimConstraint headAimConstraint;
	public MultiAimConstraint headCounterleanConstraint;
	public MultiAimConstraint upperTorsoLeanConstraint;
	public MultiAimConstraint middleTorsoLeanConstraint;

	public bool isKnockbacked = false;
	private float knockbackTimer;

	private NavMeshAgent agent;
	private Vector3 agentDestination;
	private float minKnockbackDuration = 0.000001f;

	// used to be 50f
	public float repairDelay = 10f;
	public Dictionary<RepairTarget, float> damagedLimbs = new Dictionary<RepairTarget, float>();
	private List<RepairTarget> toRepair = new List<RepairTarget>();

	public class RepairTarget
	{
		public SystemModel system { get; private set; }
		public LimbID specificLimb { get; private set; }

		public RepairTarget(SystemModel s, LimbID l = LimbID.none)
		{
			system = s;
			specificLimb = l;
		}

		public override bool Equals(object obj)
		{
			if (obj is RepairTarget other)
			{
				return system == other.system && specificLimb == other.specificLimb;
			}
			return false;
		}

		public override int GetHashCode()
		{
			int hash = system.GetHashCode();
			if (specificLimb != LimbID.none)
				hash = hash * 31 + specificLimb.GetHashCode();
			return hash;
		}
	}

	public ConfigurableJoint upperTorsoJoint;
	public ConfigurableJoint middleTorsoJoint;

	public ConfigurableJoint upperRightArmJoint;

	private JointDrive tempJoint;

	public MultiAimConstraint upperTorsoMac;
	public MultiAimConstraint lowerTorsoMac;

	public Transform taggingTarget;

	RaycastHit hit;
	public LayerMask aimMask;
	public Transform torsoAimPoint;
	private float lastRaycastTime;
	private float raycastInterval = 0.1f; // Adjust this value as needed
	public Collider[] bodyColliders; // Array to hold player's own colliders

	float currentSelfXrotation;
	float currentSelfYrotation;
	float currentXrotationRef;
	float currentYrotationRef;

	// Start is called before the first frame update
	void Start()
	{
		//InputController can be either a player or AI. We check if it's a PlayerController and
		//if it isn't we make it an AI
		if (GetComponent<PlayerController>() != null)
		{
			Debug.Log("found player controller");
			input = GetComponent<PlayerController>();
			auraManager = GetComponent<AuraManager>();
		}
		else
		{
			input = GetComponent<AIController>();
			agent = GetComponentInParent<NavMeshAgent>();
			aiHealth = 4f;
			isAI = true;
		}
		//so_initialBodyStats = (BodyInfo)Resources.Load<ScriptableObject>("PlayerStartBodyInfo");
		systemControllers = InitSystems();
		heatContainer = GetComponent<HeatContainer>();
		//heatContainer.InitCoolingModel(cooling);
		SubscribeSystemEvents();
		bodyState.Init(systemControllers, heatContainer, this);
		rb = GetComponent<Rigidbody>();
		bodyColliders = GetComponentsInChildren<Collider>();
		tempJoint = new JointDrive();

		// coolingGaugeScaleCache = coolingGauge.transform.localScale;
		taggingGaugeScaleCache = taggingGauge.transform.localScale;
		// healthIndicator.text = head.health.ToString();
	}

	List<SystemModel> InitSystems()
	{
		List<SystemModel> models = new List<SystemModel>();
		for (int i = 0; i < so_initialBodyStats.rawSystems.Length; i++)
		{
			BodyInfo.systemID sys = so_initialBodyStats.rawSystems[i];
			switch (sys)
			{
				// case BodyInfo.systemID.Cooling:
				// 	cooling = new CoolingModel(so_initialBodyStats.rawSystemStartLevels[i], rb);
				// 	models.Add(cooling);
				// 	Debug.Log("Cooling added");
				// 	break;
				case BodyInfo.systemID.Legs:
					legs = new LegsModel(so_initialBodyStats.rawSystemStartLevels[i], rb, physicalHead.transform);
					if (isAI)
					{
						legs.rightLegCurrentHealth = aiHealth / 2;
						legs.leftLegCurrentHealth = aiHealth / 2;
					}
					models.Add(legs);
					Debug.Log("Legs added");
					break;
				case BodyInfo.systemID.Sensors:
					sensors = new SensorsModel(so_initialBodyStats.rawSystemStartLevels[i], this, headObject, headObjectL);
					models.Add(sensors);
					Debug.Log("Sensors added");
					break;
				case BodyInfo.systemID.Weapons:
					weapons = new WeaponsModel(so_initialBodyStats.rawSystemStartLevels[i], guns, gunsL, weaponRigidbody);
					if (isAI) weapons.currentHealth = aiHealth / 2;
					models.Add(weapons);
					Debug.Log("Weapons added");
					break;
				case BodyInfo.systemID.Head:
					head = new HeadModel(so_initialBodyStats.rawSystemStartLevels[i]);
					if (isAI) head.currentHealth = aiHealth;
					models.Add(head);
					Debug.Log("Head added with " + head.currentHealth + " health");
					break;
				case BodyInfo.systemID.Siphon:
					siphon = new SiphonModel(so_initialBodyStats.rawSystemStartLevels[i], siphonHead, siphonArm);
					models.Add(siphon);
					Debug.Log("Siphon added");
					break;
				default:
					break;
			}
		}

		weapons.CycleToNextPowerAllocationDictionary();

		return models;
	}

	void SubscribeSystemEvents()
	{
		//weapons.RaiseFiredWeapon += heatContainer.IncreaseHeat;

		// heatContainer.OnOverheated += () => cooling.SetOverheated(true);

		// //cooling.RaiseIncreasedHeat += StopCooling;

		// heatContainer.OnOverheated += weapons.OnCoolingSystemOverheat;
		// //cooling.RaiseOverheated += weapons.OnCoolingSystemOverheat;
		// cooling.RaiseCooledDownFromOverheat += weapons.OnCoolingSystemCooledOff;

		// heatContainer.OnOverheated += legs.OnCoolingSystemOverheat;
		// //cooling.RaiseOverheated += legs.OnCoolingSystemOverheat;
		// cooling.RaiseCooledDownFromOverheat += legs.OnCoolingSystemCooledOff;


		head.RaiseDeath += Die;
	}

	SystemModel GetSystem(BodyInfo.systemID sysID)
	{
		return systemControllers.Find(s => s.name == sysID);
	}

	public void HandleDamage(DamageInfo i)
	{
		legs.HandleTagging(i.limb, i.impactForce);
		weapons.HandleDisruption(i.limb);
		// ApplyKnockback(i.impactVector, i.limb);
		// if (cooling.isOverheated)
		// {
		DamageSystem(i);
		//}

		//heatContainer.IncreaseHeat(this, i.amount);
		//cooling.IncreaseHeat(this, i.amount);
	}

	public void DamageSystem(DamageInfo i)
	{
		if (i.limb.specificLimb == Limb.LimbID.none)
		{
			head.DamageHealth(i.amount * 0.25f);
			//GetSystem(i.limb.linkedSystem).Damage(1);
			Mathf.Clamp01(bodyState.hitStunAmount += 0.2f);
			checkForRepair(i);
		}
		else
		{
			switch (i.limb.specificLimb)
			{
				case LimbID.leftLeg:
					legs.damangeLeftLegCurrentHealth(i.amount);
					head.DamageHealth(i.amount * 0.25f);
					Mathf.Clamp01(bodyState.hitStunAmount += 1f);
					checkForRepair(i);
					break;
				case LimbID.rightLeg:
					legs.damangeRightLegCurrentHealth(i.amount);
					head.DamageHealth(i.amount * 0.25f);
					Mathf.Clamp01(bodyState.hitStunAmount += 1f);
					checkForRepair(i);
					break;
				case LimbID.torso:
					head.DamageHealth(i.amount);
					// Debug.Log(LimbID.torso + " " + i.amount + " " + head.currentHealth);
					Mathf.Clamp01(bodyState.hitStunAmount += 1f);
					break;
				case LimbID.head:
					head.DamageHealth(i.amount * 2);
					// Debug.Log(LimbID.head + " " + i.amount * 2 + " " + head.currentHealth);
					//head.Damage((int)i.amount);
					Mathf.Clamp01(bodyState.hitStunAmount += 1f);
					break;
			}
		}
	}

	void checkForRepair(DamageInfo i)
	{
		RepairTarget target;
		target = new RepairTarget(GetSystem(i.limb.linkedSystem), i.limb.specificLimb);
		if (!damagedLimbs.ContainsKey(target))
		{
			damagedLimbs.Add(target, Time.time + repairDelay);
		}
		else
		{
			// Reset timer if already damaged
			damagedLimbs[target] = Time.time + repairDelay;
		}
	}

	public void doLimbRepairs()
	{
		// if (legs.leftLegHealth < legs.currentLevelWithoutDamage)
		// {
		// 	RepairTarget target;
		// 	target = new RepairTarget(legs, LimbID.leftLeg);
		// 	if (!damagedLimbs.ContainsKey(target))
		// 	{
		// 		damagedLimbs.Add(target, Time.time + repairDelay);
		// 		Debug.Log("add lleg repair");
		// 	}
		// 	else
		// 	{
		// 		Debug.Log(damagedLimbs[target] + " || " + Time.time);
		// 	}
		// }

		// if (legs.rightLegHealth < legs.currentLevelWithoutDamage)
		// {
		// 	RepairTarget target;
		// 	target = new RepairTarget(legs, LimbID.rightLeg);
		// 	if (!damagedLimbs.ContainsKey(target))
		// 	{
		// 		damagedLimbs.Add(target, Time.time + repairDelay);
		// 		Debug.Log("add rleg repair");
		// 	}
		// 	else
		// 	{
		// 		Debug.Log(damagedLimbs[target] + " || " + Time.time);
		// 	}
		// }

		foreach (var entry in damagedLimbs)
		{
			// Debug.Log(entry.Key.specificLimb + " : " + entry.Value + "-||-" + Time.time);
			SystemModel limb = entry.Key.system;
			float repairTime = entry.Value;

			if (Time.time >= repairTime)
			{
				head.Repair(1);
				if (entry.Key.specificLimb == Limb.LimbID.none)
				{
					limb.Repair(1);
				}
				else
				{
					switch (entry.Key.specificLimb)
					{
						case LimbID.leftLeg:
							legs.healLeftLeg(1);
							if ((entry.Key.specificLimb == LimbID.leftLeg && legs.leftLegHealth == limb.currentLevelWithoutDamage))
							{
								toRepair.Add(entry.Key);
								// Debug.Log("lleg done");
							}
							break;
						case LimbID.rightLeg:
							legs.healRightLeg(1);
							if ((entry.Key.specificLimb == LimbID.rightLeg && legs.rightLegHealth == limb.currentLevelWithoutDamage))
							{
								toRepair.Add(entry.Key);
								// Debug.Log("rleg done");
							}
							break;
						case LimbID.head:
							// head.Repair(1);
							break;
					}
				}
				if (limb.currentLevelWithoutDamage == limb.currentLevel && entry.Key.system != legs)
				{
					Debug.Log("Repaired " + limb.name);
					toRepair.Add(entry.Key);

					// string dict = "[";
					// foreach (RepairTarget l in toRepair)
					// {
					// 	dict += l.system + ", " + l.specificLimb + " | ";
					// }
					// dict += "]";
					// Debug.Log(dict);
				}
			}
			else
			{
				//Debug.Log("Time current: " + Time.time + " Time of repair: " + repairTime);
			}
		}
		// var dlen = toRepair.ToArray().Length;
		// Remove fully repaired limbs
		foreach (RepairTarget limb in toRepair)
		{
			// Debug.Log(limb.specificLimb + " was fully repaired");
			damagedLimbs.Remove(limb);
		}
		toRepair.Clear();

		// foreach (var entry in damagedLimbs)
		// {
		// 	Debug.Log(entry.Key.specificLimb + " : " + entry.Value + "-||-" + Time.time);
		// }

		// var dlenafter = toRepair.ToArray().Length;

		// if (dlen > dlenafter)
		// {
		// 	string dict = "[";
		// 	foreach (RepairTarget limb in toRepair)
		// 	{
		// 		dict += limb.system + ", " + limb.specificLimb + " | ";
		// 	}
		// 	dict += "]";
		// 	Debug.Log(dict);
		// }
	}

	public void Die()
	{
		isDead = true;
		bodyState.isDead = true;

		ActiveRagdollController arc = GetComponentInChildren<ActiveRagdollController>();
		arc.enabled = false;

		Debug.Log("Dead!");
		Debug.Log(GetComponentsInChildren<ConfigurableJoint>().Length);
		foreach (ConfigurableJoint j in GetComponentsInChildren<ConfigurableJoint>())
		{
			JointDrive d = new JointDrive();
			d = j.angularXDrive;
			d.positionSpring = 0;
			j.angularXDrive = d;

			d = j.angularYZDrive;
			d.positionSpring = 0;
			j.angularYZDrive = d;

			d = j.slerpDrive;
			d.positionSpring = 0;
			j.slerpDrive = d;
		}

		foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>())
		{
			//r.sleepThreshold = 0.5f;
			r.drag = 0;
			r.angularDrag = 0;
		}
		ragdollCore.isKinematic = false;
		ragdollCore.constraints = RigidbodyConstraints.None;
		//ragdollCore.AddForce(new Vector3(0, 0, -1000));
	}

	#region Inputs

	public void MoveForward()
	{
		legs.ExecuteForward();
	}

	public void MoveBackward()
	{
		legs.ExecuteBackward();
	}

	public void MoveLeft()
	{
		legs.ExecuteLeft();
	}

	public void MoveRight()
	{
		legs.ExecuteRight();
	}

	public void FireWeapon1()
	{
		if (isAimingRight || isAI)
		{
			weapons.ExecuteWeapon1(true);

		}
		else if (isAimingLeft || isAI)
		{
			weapons.ExecuteWeapon1(false);
		}
		//Debug.Log(guns.ActiveGun1.Model.transform.position);

		// TODO This is just to debug the AI cycling power allocations 
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
	}

	public void FireWeapon2()
	{
		weapons.ExecuteWeapon2();
		//Debug.Log(guns.ActiveGun2.Model.transform.position);

		// TODO This is just to debug the AI cycling power allocations 
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
	}

	public void FireWeapon3()
	{
		weapons.ExecuteWeapon3();
		//Debug.Log(guns.ActiveGun3.Model.transform.position);

		// TODO This is just to debug the AI cycling power allocations 
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
	}

	public void CycleWeaponPowerAllocation()
	{
		weapons.CycleToNextPowerAllocationDictionary();

		// TODO Temporary weapon gauge visual
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);

		//weapons.PrintPowerAllocation(weapons.GetCurrentPowerAllocation());
	}

	private void setWeaponGauges()
	{
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0] && weapons.guns[0].isCharged());
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1] && weapons.guns[1].isCharged());
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2] && weapons.guns[2].isCharged());
	}

	private void DoRotation()
	{
		if (isAimingRight || isAimingLeft)
		{
			// In aim mode, only rotate head
			sensors.setHeadRotation(input.getHeadRotation());
		}
		else
		{
			// if (input.getHeadRotation().magnitude > 0)
			// {
			// 	cameraMoveScript.enabled = true;
			// }
			// Default: rotate both body and head
			sensors.setHeadRotation(new Vector2(input.getHeadRotation().x, 0));
			transform.Rotate(0, input.getHeadRotation().y, 0);
		}
	}

	void ToggleAimingRight()
	{
		isAimingLeft = false;

		isAimingRight = !isAimingRight;
		if (isAimingRight)
		{
			if (!startedAimingRight)
			{
				startedAimingRight = true;
			}

			// headAimConstraint.data.sourceObjects.SetWeight(0, 0);
			// headAimConstraint.data.sourceObjects.SetWeight(1, 1);
			// headAimConstraint.data.sourceObjects.SetWeight(2, 0);


			var a = headAimConstraint.data.sourceObjects;
			var a0 = a[0];
			var a1 = a[1];
			var a2 = a[2];
			a0.weight = 0;
			a1.weight = 1;
			a2.weight = 0;
			a[0] = a0;
			a[1] = a1;
			a[2] = a2;
			headAimConstraint.data.sourceObjects = a;
		}
		else
		{
			// headAimConstraint.data.sourceObjects.SetWeight(0, 1);
			// headAimConstraint.data.sourceObjects.SetWeight(1, 0);
			// headAimConstraint.data.sourceObjects.SetWeight(2, 0);

			var a = headAimConstraint.data.sourceObjects;
			var a0 = a[0];
			var a1 = a[1];
			var a2 = a[2];
			a0.weight = 1;
			a1.weight = 0;
			a2.weight = 0;
			a[0] = a0;
			a[1] = a1;
			a[2] = a2;
			headAimConstraint.data.sourceObjects = a;
		}
	}

	void ToggleAimingLeft()
	{
		isAimingRight = false;

		isAimingLeft = !isAimingLeft;
		if (isAimingLeft)
		{
			if (!startedAimingLeft)
			{
				startedAimingLeft = true;
			}

			// headAimConstraint.data.sourceObjects.SetWeight(0, 0);
			// headAimConstraint.data.sourceObjects.SetWeight(1, 0);
			// headAimConstraint.data.sourceObjects.SetWeight(2, 1);

			var a = headAimConstraint.data.sourceObjects;
			var a0 = a[0];
			var a1 = a[1];
			var a2 = a[2];
			a0.weight = 0;
			a1.weight = 0;
			a2.weight = 1;
			a[0] = a0;
			a[1] = a1;
			a[2] = a2;
			headAimConstraint.data.sourceObjects = a;
		}
		else
		{
			// headAimConstraint.data.sourceObjects.SetWeight(0, 1);
			// headAimConstraint.data.sourceObjects.SetWeight(1, 0);
			// headAimConstraint.data.sourceObjects.SetWeight(2, 0);

			var a = headAimConstraint.data.sourceObjects;
			var a0 = a[0];
			var a1 = a[1];
			var a2 = a[2];
			a0.weight = 1;
			a1.weight = 0;
			a2.weight = 0;
			a[0] = a0;
			a[1] = a1;
			a[2] = a2;
			headAimConstraint.data.sourceObjects = a;
		}
	}

	private void GetAimPoint()
	{
		// if (rb.velocity.magnitude > 0.2f && isAimingRight)
		// {
		// 	// moving → reset weapon aim to torso
		// 	weaponAimPoint.position = torsoAimPoint.position;
		// 	aimCam.transform.rotation = headObjectTransformCache.transform.rotation;
		// 	ToggleAiming();
		// 	return;
		// }
		//		if (Time.time - lastRaycastTime >= raycastInterval && rb.velocity.magnitude < 0.2f)

		if (Time.time - lastRaycastTime >= raycastInterval)
		{
			//			Vector3 torso = aimCam.transform.position + 20 * aimCam.transform.forward;

			// Vector3 torso = transform.position + 20 * transform.forward;

			// --- Torso Aim Calculation --- //

			// Step 1: get the torso’s yaw only (ignore pitch/roll)
			Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

			// Step 2: get pitch from the camera (local, relative to torso/head)
			float pitch = aimCam.transform.localEulerAngles.x;

			// Unity’s localEulerAngles.x can wrap around past 180, so normalize it:
			if (pitch > 180f) pitch -= 360f;

			// Step 3: combine yaw + pitch into a clean orientation
			Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);

			// Step 4: get forward from this rotation
			Vector3 pitchedForward = combinedRot * Vector3.forward;

			// Step 5: place the torso aim point forward from the head
			Vector3 torso = headObjectTransformCache.position + pitchedForward * 20f;

			// Only reset weaponAimPoint if MOVING while not aiming
			if (rb.velocity.magnitude > 2.5f)
			{
				torsoAimPoint.position = torso;
				if (!freezeHeadDuringMoveAimYaw)
				{
					weaponAimPoint.position = torso;
					weaponAimPointL.position = torso;
				}
				return;
			}

			if (freezeHeadDuringMoveAimYaw)
			{
				weaponAimPoint.position = torso;
				weaponAimPointL.position = torso;
				torsoAimPoint.position = torso;
				return;
			}

			// If we’re not aiming but standing still → don’t overwrite weaponAimPoint.
			// Just keep updating torsoAimPoint.
			if (!isAimingRight || !isAimingLeft)
			{
				torsoAimPoint.position = torso;
				if (!startedAimingRight && !startedAimingLeft)
				{
					weaponAimPoint.position = torso;
					weaponAimPointL.position = torso;
					return;
				}

				if (startedAimingRight && !isAimingRight)
				{
					// headObjectAimOffset.position.Set(headObjectAimOffset.position.x, headObjectAimOffset.position.y, Vector3.Distance(weaponAimPoint.position, headObject.transform.position));
					weaponAimPoint.position = headObjectAimOffset.position;

				}
				if (startedAimingLeft && !isAimingLeft)
				{
					// headObjectAimOffsetL.position.Set(headObjectAimOffsetL.position.x, headObjectAimOffsetL.position.y, Vector3.Distance(weaponAimPointL.position, headObject.transform.position));
					weaponAimPointL.position = headObjectAimOffsetL.position;
				}
			}


			if (isAimingRight || isAimingLeft)
			{
				// --- Aim mode: full yaw + pitch from camera ---
				combinedRot = Quaternion.Euler(aimCam.transform.eulerAngles.x,
																			 aimCam.transform.eulerAngles.y,
																			 0f);
			}
			else
			{
				// Debug.Log("hipfire");

				// --- Hipfire: yaw from torso, pitch from camera ---
				torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

				pitch = aimCam.transform.localEulerAngles.x;
				if (pitch > 180f) pitch -= 360f;

				combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);

				if (!startedAimingRight && !startedAimingLeft && !freezeHeadDuringMoveAimYaw)
				{
					Debug.Log("setting head to cache");
					headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
				}
			}

			// Forward from combined rotation
			Vector3 forward = combinedRot * Vector3.forward;

			// Torso aim point
			torso = headObjectTransformCache.position + forward * 20f;

			// Raycast for weapon aim
			Ray ray = new Ray(physicalHead.transform.position, forward);
			RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, aimMask);


			if (hits.Length <= 0)
			{
				weaponAimPoint.position = torso;
				torsoAimPoint.position = torso;
			}
			else
			{
				RaycastHit? bodyHit = null;
				List<RaycastHit> enviroHits = new List<RaycastHit>();

				foreach (var hit in hits)
				{
					bool isOwnCollider = false;

					if (hit.collider.gameObject.layer == 9)
					{
						enviroHits.Add(hit);
						continue;
					}

					// Check if the hit collider belongs to the player
					foreach (var collider in bodyColliders)
					{
						if (hit.collider == collider)
						{
							isOwnCollider = true;
							break;
						}
					}

					// if (bodyHit.HasValue)
					// {
					// 	Vector3 targetPoint = bodyHit.Value.point;
					// 	// Rotate the arm/gun to aim at the targetPoint
					// 	weaponAimPoint.position = targetPoint;
					// 	return;
					// }

					// If the hit collider does not belong to the player, set it as the aim target
					if (!isOwnCollider && hit.collider.gameObject.layer == 6)
					{
						//Debug.Log(hit.collider.gameObject.layer);
						bodyHit = hit;
						break; // Exit the loop after finding the first valid aim target
					}
				}

				if (bodyHit.HasValue)
				{
					if (enviroHits.Count > 0)
					{
						enviroHits.Sort((hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

						if (Vector3.Distance(rb.transform.position, bodyHit.Value.point) < Vector3.Distance(rb.transform.position, enviroHits[0].point))
						{
							Vector3 targetPoint = bodyHit.Value.point;

							if (isAimingRight)
							{
								weaponAimPoint.position = targetPoint;
							}
							else if (isAimingLeft)
							{
								weaponAimPointL.position = targetPoint;
							}
						}
						else
						{
							Vector3 targetPoint = enviroHits[0].point;
							// Rotate the arm/gun to aim at the targetPoint
							if (isAimingRight)
							{
								weaponAimPoint.position = targetPoint;
							}
							else if (isAimingLeft)
							{
								weaponAimPointL.position = targetPoint;
							}
						}
					}
					// Vector3 targetPoint = bodyHit.Value.point;
					// // Rotate the arm/gun to aim at the targetPoint
					// weaponAimPoint.position = targetPoint;
				}
				else if (enviroHits.Count > 0)
				{
					enviroHits.Sort((hit1, hit2) => hit1.distance.CompareTo(hit2.distance));
					// Use the closest hit's point as the target point
					Vector3 targetPoint = enviroHits[0].point;
					// Rotate the arm/gun to aim at the targetPoint
					if (isAimingRight)
					{
						weaponAimPoint.position = targetPoint;
					}
					else if (isAimingLeft)
					{
						weaponAimPointL.position = targetPoint;
					}
				}
				else
				{
					//weaponAimPoint.position = Vector3.Lerp(weaponAimPoint.position, torso, 0.2f);
					weaponAimPoint.position = torso;
					weaponAimPointL.position = torso;

				}
			}
			torsoAimPoint.position = torso;
		}
		else
		{
			if (!freezeHeadDuringMoveAimYaw)
			{
				ResetWeaponAimPoint();
			}
		}


		// Vector3 torso = aimCam.transform.position + 20 * aimCam.transform.forward;
		// if (Physics.Raycast(aimCam.transform.position, aimCam.transform.forward, out hit, Mathf.Infinity, aimMask))
		// {
		// 	//Debug.DrawRay(headObject.transform.position, headObject.transform.TransformDirection(Vector3.forward) * hit.distance, Color.yellow);
		// 	//Debug.Log(hit.distance);
		// 	//weaponAimPoint.position = Vector3.Lerp(weaponAimPoint.position, hit.point, 0.2f);
		// 	weaponAimPoint.position = hit.point;
		// }
		// else
		// {
		// 	//weaponAimPoint.position = Vector3.Lerp(weaponAimPoint.position, torso, 0.2f);
		// 	weaponAimPoint.position = torso;
		// }
		// torsoAimPoint.position = torso;
	}

	void ResetWeaponAimPoint(bool resetPitch = false, bool resetHead = true)
	{
		// Debug.Log("resetting aim");

		// --- Torso Aim Calculation --- //

		// Step 1: get the torso’s yaw only (ignore pitch/roll)
		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

		// Step 2: get pitch from the camera (local, relative to torso/head)
		float pitch = aimCam.transform.localEulerAngles.x;

		// Unity’s localEulerAngles.x can wrap around past 180, so normalize it:
		if (pitch > 180f) pitch -= 360f;

		// Step 3: combine yaw + pitch into a clean orientation
		Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);

		// Step 4: get forward from this rotation
		Vector3 pitchedForward = combinedRot * Vector3.forward;

		// Step 5: place the torso aim point forward from the head
		Vector3 torso = headObjectTransformCache.position + pitchedForward * 20f;

		weaponAimPoint.position = torso;
		weaponAimPointL.position = torso;
		torsoAimPoint.position = torso;
		if (resetHead)
		{
			headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
			headObjectL.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
		}
		if (resetPitch && sensors != null)
		{
			sensors.ResetHeadPitch();
		}


		// cameraMoveScript.enabled = false;
		// // headObject.transform.rotation = headObjectTransformCache.transform.rotation;
		// aimCam.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
		// cameraMoveScript.enabled = true;
	}

	#endregion

	void HandleScrollUp()
	{
		if (isAimingRight)
		{
			ToggleAimingRight();
			return;
		}

		if (!isAimingLeft && !isAimingRight)
		{
			ToggleAimingLeft();
			return;
		}
	}

	void HandleScrollDown()
	{
		if (isAimingLeft)
		{
			ToggleAimingLeft();
			return;
		}

		if (!isAimingLeft && !isAimingRight)
		{
			ToggleAimingRight();
			return;
		}
	}

	void HandleMiddleClick()
	{
		if (isAimingLeft)
		{
			ToggleAimingLeft();
			return;
		}

		if (isAimingRight)
		{
			ToggleAimingRight();
			return;
		}

	}

	// private void doCooling()
	// {
	// 	// if (cooling.isOverheated)
	// 	// {
	// 	// 	cooling.CooldownOverheated();
	// 	// }
	// 	// else
	// 	// {
	// 	if (rb.velocity.magnitude < 0.05f)
	// 	{
	// 		cooling.SetStandingStill(true);
	// 	}
	// 	else
	// 	{
	// 		cooling.SetStandingStill(false);
	// 	}
	// 	// 	else
	// 	// 	{
	// 	// 		cooling.Cooldown();
	// 	// 	}
	// 	// }
	// 	// cooling.PassiveCooldown();

	// 	//TODO Temporary heating gauge visual
	// 	//coolingGauge.transform.localScale = coolingGaugeScaleCache * Mathf.Clamp((heatContainer.currentTemperature + 0.01f) / cooling.GetMaxHeat(), 0, 1f);
	// }

	private Vector3 KnockbackHeightCheck = new Vector3(0, 1f, 0);
	// private void ApplyKnockback(Vector3 force, Limb l)
	// {
	// 	isKnockbacked = true;
	// 	if (isAI)
	// 	{
	// 		agentDestination = agent.destination;
	// 	}
	// 	// if (agent != null)
	// 	// {
	// 	// 	agent.enabled = false;
	// 	// }

	// 	bool backTooCloseToWall = false;
	// 	RaycastHit hit;
	// 	if (Physics.Raycast(transform.position + KnockbackHeightCheck, -transform.forward, out hit, 2.0f, aimMask))
	// 	{
	// 		backTooCloseToWall = true;
	// 	}

	// 	if (!backTooCloseToWall)
	// 	{
	// 		if (cooling.isOverheated)
	// 		{
	// 			rb.AddForce((force * 2));

	// 		}
	// 		else
	// 		{
	// 			rb.AddForce((force / 3) * (1 - legs.getTagging()) * GetKnockbackFromLimb(l));
	// 		}
	// 	}
	// 	else
	// 	{
	// 		rb.AddForce((force / 8) * (1 - legs.getTagging()) * GetKnockbackFromLimb(l));
	// 	}
	// 	knockbackTimer = minKnockbackDuration;
	// }

	public float GetKnockbackFromLimb(Limb l)
	{
		switch (l.specificLimb)
		{
			case Limb.LimbID.leftLeg:
				return 0.5f;
			case Limb.LimbID.rightLeg:
				return 0.5f;
			case Limb.LimbID.torso:
				return 1f;
			case Limb.LimbID.head:
				return 0.75f;
			default:
				return 0.2f;
		}
	}

	private void HandleKnockback()
	{
		if (knockbackTimer > 0 && isKnockbacked == true)
		{
			knockbackTimer -= Time.deltaTime;
		}
		else
		{
			if (rb.velocity.magnitude < 0.05f && rb.velocity.y < 0.01f && isKnockbacked == true)
			{
				NavMeshAgent agent = GetComponentInParent<NavMeshAgent>();
				isKnockbacked = false;
				rb.velocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
				transform.localPosition.Set(0, transform.localPosition.y, 0);
				// Transform parentTransform = GetComponentInParent<Transform>();
				// parentTransform.position = transform.position;
				if (agent != null)
				{
					float yPos = transform.localPosition.y;
					agent.enabled = true;
					agent.Warp(rb.transform.position);
					rb.transform.localPosition = new Vector3(0, yPos, 0);
					// Debug.Log(agent.hasPath);
					agent.ResetPath();
					agent.SetDestination(agentDestination);
					//this.transform.SetParent(parent.transform, true);
				}
			}
		}
		// if (agent != null && Vector3.Distance(agent.transform.position, rb.transform.position) > 0.01f)
		// {
		// 	agent.Warp(rb.transform.position);
		// }
	}

	private void ClampRigidbodyYPos()
	{
		float yPos = Mathf.Clamp(rb.transform.position.y, 1.6f, Mathf.Infinity);
		rb.transform.position = new Vector3(rb.transform.position.x, yPos, rb.transform.position.z);
	}

	private void doSiphoning()
	{
		if (input.getSiphon())
		{
			siphon.ToggleSiphon();
		}
		else
		{
			siphon.NotSiphoning();
		}
	}

	private void DoReload()
	{
		if (!isAimingLeft && !isAimingRight)
		{
			gunsL.ActiveGun1.StartReload();
			guns.ActiveGun1.StartReload();
		}

		if (isAimingLeft)
		{
			gunsL.ActiveGun1.StartReload();
		}
		if (isAimingRight)
		{
			guns.ActiveGun1.StartReload();
		}
	}

	private void setJointStrength()
	{
		// float overheated = 1f;

		tempJoint = upperTorsoJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp(((1 - bodyState.hitStunAmount) * 100000) + 2000, 1000, 100000);
		upperTorsoJoint.slerpDrive = tempJoint;

		tempJoint = middleTorsoJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp(((1 - bodyState.hitStunAmount) * 100000) + 2000, 1000, 100000);
		middleTorsoJoint.slerpDrive = tempJoint;

		tempJoint = upperRightArmJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp(((1 - bodyState.hitStunAmount) * 100000) + 2000, 1000, 100000);
		upperRightArmJoint.slerpDrive = tempJoint;

		//TODO Temporary tagging gauge visual
		//taggingGauge.transform.localScale = taggingGaugeScaleCache * Mathf.Clamp((legs.taggingModifier + 0.01f) / 100f, 0, 1f);
	}

	private void SetRigPosture()
	{
		float posture = 1 - bodyState.hitStunAmount;

		upperTorsoMac.data.sourceObjects.SetWeight(0, posture);
		upperTorsoMac.data.sourceObjects.SetWeight(1, 1 - posture);

		lowerTorsoMac.data.sourceObjects.SetWeight(0, posture);
		lowerTorsoMac.data.sourceObjects.SetWeight(1, 1 - posture);

		var a = upperTorsoMac.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		a0.weight = posture;
		a1.weight = 1 - posture;
		a[0] = a0;
		a[1] = a1;
		upperTorsoMac.data.sourceObjects = a;

		a = lowerTorsoMac.data.sourceObjects;
		a0 = a[0];
		a1 = a[1];
		a0.weight = posture;
		a1.weight = 1 - posture;
		a[0] = a0;
		a[1] = a1;
		lowerTorsoMac.data.sourceObjects = a;

		taggingTarget.rotation = Quaternion.Euler(320 + (30 * (1 - posture)), 0, 180);
	}

	public static float ExpDamp(float current, float target, float lambda)
	{
		return Mathf.Lerp(current, target, 1f - Mathf.Exp(-lambda * Time.deltaTime));
	}
	float leanLambda = 8f; // higher = snappier

	private void LeanLeft()
	{
		// Debug.Log("in LeanLeft");
		if (!startedLeaningLeft)
		{
			startedLeaningLeft = true;
		}

		Vector3 bodyForward = transform.forward;
		Vector3 headForward = isAimingLeft ? headObjectL.transform.forward : headObject.transform.forward;

		float headYaw = Vector3.SignedAngle(bodyForward, headForward, Vector3.up);

		if (headYaw > 180) headYaw -= 360;

		Vector3 leanDir = Vector3.left;

		// Rotate leanDir around Y axis by headYaw degrees
		float radians = headYaw * Mathf.Deg2Rad;

		// Rotate around Y axis (Unity coordinate system)
		float rotatedX = leanDir.x * Mathf.Cos(radians) + leanDir.z * Mathf.Sin(radians);
		float rotatedZ = leanDir.z * Mathf.Cos(radians) - leanDir.x * Mathf.Sin(radians);

		// Map rotated direction to 4 weights (front, back, left, right)
		float front = Mathf.Max(0, rotatedZ);
		float back = Mathf.Max(0, -rotatedZ);
		float right = Mathf.Max(0, rotatedX);
		float left = Mathf.Max(0, -rotatedX);

		// Normalize so all weights sum to 1
		float total = front + back + left + right;
		if (total > 0)
		{
			front /= total;
			back /= total;
			left /= total;
			right /= total;
		}

		var a = upperTorsoLeanConstraint.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		var a2 = a[2];
		var a3 = a[3];
		// a0.weight = a0.weight < left ? a0.weight + leanSpeed : left;
		// a1.weight = a1.weight < right ? a1.weight + leanSpeed : right;
		// a2.weight = a2.weight < front ? a2.weight + leanSpeed : front;
		// a3.weight = a3.weight < back ? a3.weight + leanSpeed : back;

		a0.weight = ExpDamp(a0.weight, left, leanLambda);
		a1.weight = ExpDamp(a1.weight, right, leanLambda);
		a2.weight = ExpDamp(a2.weight, front, leanLambda);
		a3.weight = ExpDamp(a3.weight, back, leanLambda);
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		upperTorsoLeanConstraint.data.sourceObjects = a;

		a = middleTorsoLeanConstraint.data.sourceObjects;
		a0 = a[0];
		a1 = a[1];
		a2 = a[2];
		a3 = a[3];
		// a0.weight = a0.weight < left ? a0.weight + leanSpeed : left;
		// a1.weight = a1.weight < right ? a1.weight + leanSpeed : right;
		// a2.weight = a2.weight < front ? a2.weight + leanSpeed : front;
		// a3.weight = a3.weight < back ? a3.weight + leanSpeed : back;

		a0.weight = ExpDamp(a0.weight, left, leanLambda);
		a1.weight = ExpDamp(a1.weight, right, leanLambda);
		a2.weight = ExpDamp(a2.weight, front, leanLambda);
		a3.weight = ExpDamp(a3.weight, back, leanLambda);
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		middleTorsoLeanConstraint.data.sourceObjects = a;
	}

	private void LeanRight()
	{
		// Debug.Log("in LeanLeft");
		if (!startedLeaningRight)
		{
			startedLeaningRight = true;
		}

		Vector3 bodyForward = transform.forward;
		Vector3 headForward = isAimingLeft ? headObjectL.transform.forward : headObject.transform.forward;

		float headYaw = Vector3.SignedAngle(bodyForward, headForward, Vector3.up);

		if (headYaw > 180) headYaw -= 360;
		Vector3 leanDir = Vector3.right;

		// Rotate leanDir around Y axis by headYaw degrees
		float radians = headYaw * Mathf.Deg2Rad;

		// Rotate around Y axis (Unity coordinate system)
		float rotatedX = leanDir.x * Mathf.Cos(radians) + leanDir.z * Mathf.Sin(radians);
		float rotatedZ = leanDir.z * Mathf.Cos(radians) - leanDir.x * Mathf.Sin(radians);

		// Map rotated direction to 4 weights (front, back, left, right)
		float front = Mathf.Max(0, rotatedZ);
		float back = Mathf.Max(0, -rotatedZ);
		float right = Mathf.Max(0, rotatedX);
		float left = Mathf.Max(0, -rotatedX);

		// Normalize so all weights sum to 1
		float total = front + back + left + right;
		if (total > 0)
		{
			front /= total;
			back /= total;
			left /= total;
			right /= total;
		}

		var a = upperTorsoLeanConstraint.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		var a2 = a[2];
		var a3 = a[3];

		a0.weight = ExpDamp(a0.weight, left, leanLambda);
		a1.weight = ExpDamp(a1.weight, right, leanLambda);
		a2.weight = ExpDamp(a2.weight, front, leanLambda);
		a3.weight = ExpDamp(a3.weight, back, leanLambda);
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		upperTorsoLeanConstraint.data.sourceObjects = a;

		a = middleTorsoLeanConstraint.data.sourceObjects;
		a0 = a[0];
		a1 = a[1];
		a2 = a[2];
		a3 = a[3];

		a0.weight = ExpDamp(a0.weight, left, leanLambda);
		a1.weight = ExpDamp(a1.weight, right, leanLambda);
		a2.weight = ExpDamp(a2.weight, front, leanLambda);
		a3.weight = ExpDamp(a3.weight, back, leanLambda);
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		middleTorsoLeanConstraint.data.sourceObjects = a;
	}

	void StopLeaning()
	{
		startedLeaningLeft = false;
		startedLeaningRight = false;

		var a = upperTorsoLeanConstraint.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		var a2 = a[2];
		var a3 = a[3];
		a0.weight = ExpDamp(a0.weight, 0, leanLambda);
		a1.weight = ExpDamp(a1.weight, 0, leanLambda);
		a2.weight = ExpDamp(a2.weight, 0, leanLambda);
		a3.weight = ExpDamp(a3.weight, 0, leanLambda);
		// a0.weight = a0.weight > 0 ? a0.weight - leanRecoverySpeed : 0;
		// a1.weight = a1.weight > 0 ? a1.weight - leanRecoverySpeed : 0;
		// a2.weight = a2.weight > 0 ? a2.weight - leanRecoverySpeed : 0;
		// a3.weight = a3.weight > 0 ? a3.weight - leanRecoverySpeed : 0;
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		upperTorsoLeanConstraint.data.sourceObjects = a;

		a = middleTorsoLeanConstraint.data.sourceObjects;
		a0 = a[0];
		a1 = a[1];
		a2 = a[2];
		a3 = a[3];
		a0.weight = ExpDamp(a0.weight, 0, leanLambda);
		a1.weight = ExpDamp(a1.weight, 0, leanLambda);
		a2.weight = ExpDamp(a2.weight, 0, leanLambda);
		a3.weight = ExpDamp(a3.weight, 0, leanLambda);
		// a0.weight = a0.weight > 0 ? a0.weight - leanRecoverySpeed : 0;
		// a1.weight = a1.weight > 0 ? a1.weight - leanRecoverySpeed : 0;
		// a2.weight = a2.weight > 0 ? a2.weight - leanRecoverySpeed : 0;
		// a3.weight = a3.weight > 0 ? a3.weight - leanRecoverySpeed : 0;
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		a[3] = a3;
		middleTorsoLeanConstraint.data.sourceObjects = a;
	}

	public float getAuraDamageMultipler()
	{
		return auraManager.AuraFloat;
	}

	//public void StartCooling()
	//{
	//    if (decrementCoroutine == null)
	//    {
	//        decrementCoroutine = StartCoroutine(cooling.DecreaseHeatCoroutine());
	//    }
	//}

	// public void StopCooling()
	// {
	// 	cooling.ResetCooldown();
	// }

	// Update is called once per frame
	void Update()
	{
	}

	private void FixedUpdate()
	{
		if (!isDead)
		{
			ExecutePhysicsBasedInputs();
			GetAimPoint();
			doSiphoning();
			doLimbRepairs();
			DoRotation();
			UpdatePendingMoveAimYaw();
		}
		legs.DoMoveDeacceleration();
		legs.RecoverFromTagging(1);
		legs.UpdateMovementTick(Time.deltaTime);
		weapons.RecoverFromDisruption();
		// doCooling();
		// HandleKnockback();
		ClampRigidbodyYPos();
		if (isAI)
		{
			setJointStrength();
			// SetRigPosture();
		}


		setWeaponGauges();
		// dollarsIndicator.text = (Mathf.Round(siphon.dollars * 100f) / 100f).ToString();
		// healthIndicator.text = head.health.ToString();

		if (!isAI && ((PlayerController)input).getRestart())
		{
			((PlayerController)input).doRestart();
		}
		// if (isAI)
		// {
		// 	Debug.Log(agent.hasPath);
		// }
	}

	private void ExecutePhysicsBasedInputs()
	{
		if (legs.isCurrentVelocityLessThanMax())
		{
			if (input.getForward()) MoveForward();
			if (input.getBackward()) MoveBackward();
			if (input.getLeft()) MoveLeft();
			if (input.getRight()) MoveRight();
		}

		if (rb.velocity.magnitude < 2.5f)
		{
			if (input.getFire1()) FireWeapon1();
			if (input.getFire2()) FireWeapon2();
			if (input.getFire3()) FireWeapon3();
		}
		else if (isAimingRight || isAimingLeft)
		{
			// Debug.Log("resetting aim on movement");
			if (!hasPendingMoveAimYaw)
			{
				RotateTorsoToActiveAimYaw();
				pendingMoveAimToggleOff = true;
			}

			// 
			// aimCam.transform.rotation = headObjectTransformCache.transform.rotation;
		}
		else
		{
			ResetWeaponAimPoint();
			startedAimingRight = false;
			startedAimingLeft = false;

			// cameraMoveScript.enabled = false;
			// aimCam.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
			//headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
		}

		if (input.getScrollUp()) HandleScrollUp();
		if (input.getScrollDown()) HandleScrollDown();
		if (input.getAimMiddle()) HandleMiddleClick();

		if (input.getReload()) DoReload();

		if ((!input.getAimLeft() && !input.getAimRight()) || (input.getAimLeft() && input.getAimRight()))
		{
			StopLeaning();

		}
		else if (input.getAimLeft())
		{
			LeanLeft();
		}
		else if (input.getAimRight())
		{
			LeanRight();
		}


		//if (input.getScroll()) CycleWeaponPowerAllocation();
	}

	private void RotateTorsoToActiveAimYaw()
	{
		if (hasPendingMoveAimYaw)
		{
			return;
		}
		Transform aimPoint = null;
		if (isAimingRight)
		{
			aimPoint = weaponAimPoint;
		}
		else if (isAimingLeft)
		{
			aimPoint = weaponAimPointL;
		}

		if (aimPoint == null)
		{
			return;
		}

		Quaternion target;
		if (aimCam != null)
		{
			target = Quaternion.Euler(0f, aimCam.transform.eulerAngles.y, 0f);
		}
		else
		{
			Vector3 origin = headObjectTransformCache != null ? headObjectTransformCache.position : transform.position;
			if (physicalHead != null)
			{
				origin = physicalHead.transform.position;
			}

			Vector3 dir = aimPoint.position - origin;
			dir.y = 0f;
			if (dir.sqrMagnitude < 0.0001f)
			{
				return;
			}

			target = Quaternion.LookRotation(dir.normalized, Vector3.up);
		}
		pendingMoveAimYawStart = transform.rotation;
		pendingMoveAimYaw = target;
		pendingMoveAimYawElapsed = 0f;
		hasPendingMoveAimYaw = true;
		freezeHeadDuringMoveAimYaw = true;
		moveAimYawSourceIsLeft = isAimingLeft;
		moveAimYawSourceWasRight = isAimingRight;
		if (aimCam != null)
		{
			frozenCameraRotation = aimCam.transform.rotation;
			hasFrozenCameraRotation = true;
		}
		if (headObject != null)
		{
			frozenHeadRotation = headObject.transform.rotation;
		}
		if (headObjectL != null)
		{
			frozenHeadLRotation = headObjectL.transform.rotation;
		}
	}

	private void UpdatePendingMoveAimYaw()
	{
		if (!hasPendingMoveAimYaw)
		{
			return;
		}

		if (freezeHeadDuringMoveAimYaw)
		{
			if (headObject != null)
			{
				headObject.transform.rotation = frozenHeadRotation;
			}
			if (headObjectL != null)
			{
				headObjectL.transform.rotation = frozenHeadLRotation;
			}
		}

		if (moveAimYawDuration <= 0f)
		{
			transform.rotation = pendingMoveAimYaw;
			hasPendingMoveAimYaw = false;
			freezeHeadDuringMoveAimYaw = false;
			hasFrozenCameraRotation = false;
			if (pendingMoveAimToggleOff)
			{
				if (moveAimYawSourceWasRight) ToggleAimingRight();
				if (moveAimYawSourceIsLeft) ToggleAimingLeft();
				startedAimingRight = false;
				startedAimingLeft = false;
				pendingMoveAimToggleOff = false;
			}
			ResetWeaponAimPoint(true, true);
			return;
		}

		pendingMoveAimYawElapsed += Time.deltaTime;
		float t = Mathf.Clamp01(pendingMoveAimYawElapsed / moveAimYawDuration);
		float curvedT = moveAimYawCurve != null ? moveAimYawCurve.Evaluate(t) : t;
		transform.rotation = Quaternion.Slerp(pendingMoveAimYawStart, pendingMoveAimYaw, curvedT);

		if (t >= 1f || Quaternion.Angle(transform.rotation, pendingMoveAimYaw) <= moveAimYawCompleteAngle)
		{
			hasPendingMoveAimYaw = false;
			freezeHeadDuringMoveAimYaw = false;
			hasFrozenCameraRotation = false;
			if (pendingMoveAimToggleOff)
			{
				if (moveAimYawSourceWasRight) ToggleAimingRight();
				if (moveAimYawSourceIsLeft) ToggleAimingLeft();
				startedAimingRight = false;
				startedAimingLeft = false;
				pendingMoveAimToggleOff = false;
			}
			ResetWeaponAimPoint(true, true);
		}
	}
}
