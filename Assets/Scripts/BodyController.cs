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
	private const int PlayerDamageAuraRewardTenths = 1;
	private const int PlayerKillAuraRewardTenths = 10;

	[Header("Reload Audio")]
	[Min(0f)]
	public float dualReloadAudioStaggerSeconds = 0.5f;

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
	public float torsoYawFollowsMouseMoveThreshold = 0.1f;
	public float torsoYawFollowsMouseStopThreshold = 0.05f;
	public float torsoYawFollowSmoothing = 12f;
	private bool hasPendingMoveAimYaw = false;
	private Quaternion pendingMoveAimYaw;
	private Quaternion pendingMoveAimYawStart;
	private float pendingMoveAimYawElapsed = 0f;
	private bool freezeHeadDuringMoveAimYaw = false;
	private bool movingAimYawActive = false;
	private float smoothedMovingAimYaw = 0f;
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
	public bool HasStartedAimingRight => startedAimingRight;
	public bool HasStartedAimingLeft => startedAimingLeft;
	public bool KeepCameraAimWithoutArm => keepCameraAimWithoutArm;
	public bool KeepCameraAimUsesLeft => keepCameraAimUsesLeft;
	public bool IsCenteredAim => IsPlayerCenteredAim();
	public bool PrimaryAimUsesLeft => offhandMirrorActive ? offhandMirrorSourceIsLeft : IsActiveArmLeft();
	public bool CameraAimUsesLeft => offhandMirrorActive
		? offhandMirrorCameraUsesLeft
		: isAimingLeft || (keepCameraAimWithoutArm && keepCameraAimUsesLeft);
	public bool IsRightArmAimed => IsPlayerCenteredAim() || isAimingRight || (offhandMirrorActive && offhandMirrorSourceIsLeft);
	public bool IsLeftArmAimed => IsPlayerCenteredAim() || isAimingLeft || (offhandMirrorActive && !offhandMirrorSourceIsLeft);
	private const float SlowMoveSpeedMultiplier = 0.3f;

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
	[SerializeField] private MovePlayerCamera cameraMoveScript;
	public bool isAimingRight = false;
	bool startedAimingRight = false;
	public bool isAimingLeft = false;
	bool startedAimingLeft = false;
	private bool keepCameraAimWithoutArm = false;
	private bool keepCameraAimUsesLeft = false;
	private bool forceAimToTorsoRight = false;
	private bool forceAimToTorsoLeft = false;
	private bool useStoredAimRight = false;
	private bool useStoredAimLeft = false;
	private bool hasStoredRelativeAimRight = false;
	private bool hasStoredRelativeAimLeft = false;
	private Vector3 storedRelativeAimRightLocal;
	private Vector3 storedRelativeAimLeftLocal;
	private bool offhandMirrorActive = false;
	private bool offhandMirrorSourceIsLeft = false;
	private bool offhandMirrorCameraUsesLeft = false;
	private Vector3 offhandMirrorStoredAimPoint;
	private bool offhandMirrorStoredAimingRight = false;
	private bool offhandMirrorStoredAimingLeft = false;
	private bool offhandMirrorStoredStartedRight = false;
	private bool offhandMirrorStoredStartedLeft = false;
	private bool offhandMirrorStoredKeepCameraAimWithoutArm = false;
	private bool offhandMirrorStoredKeepCameraAimUsesLeft = false;
	private bool offhandMirrorRestoreOffhandOnRelease = true;
	[Header("Aim Start Hold")]
	public float aimStartHoldDuration = 0.05f;
	[SerializeField, Min(0f)] private float aimStartReleaseInputDeadzone = 0.01f;
	[SerializeField] private float breakoutAimYawOffset = 45f;
	[SerializeField] private bool breakoutAimAssistEnabled = true;
	[SerializeField] private LayerMask breakoutAimAssistLayerMask = 1 << 6;
	[SerializeField, Min(0f)] private float breakoutAimAssistBoxSideWidth = 12f;
	[SerializeField, Min(0f)] private float breakoutAimAssistBoxDepth = 20f;
	[SerializeField, Min(0f)] private float breakoutAimAssistBoxHeight = 4f;
	[SerializeField] private float breakoutAimAssistForwardOffset = 6f;
	[SerializeField, Min(0f)] private float breakoutAimAssistSideOffset = 6f;
	[SerializeField, Min(1)] private int breakoutAimAssistMaxTargets = 16;
	[SerializeField] private bool breakoutAimAssistRequireLineOfSight = false;
	[SerializeField] private LayerMask breakoutAimAssistObstructionMask = 1 << 9;
	private Vector2 fixedTickHeadRotation;
	[Header("Breakout Aim Assist Debug")]
	[SerializeField] private bool showBreakoutAimAssistDebugVolumes = true;
	[SerializeField, Range(0f, 1f)] private float breakoutAimAssistDebugAlpha = 0.12f;
	[SerializeField] private Color breakoutAimAssistDebugLeftColor = new Color(0.2f, 0.7f, 1f, 0.12f);
	[SerializeField] private Color breakoutAimAssistDebugRightColor = new Color(1f, 0.5f, 0.2f, 0.12f);
	private float aimStartHoldTimerRight = 0f;
	private float aimStartHoldTimerLeft = 0f;
	private Vector3 aimStartHoldPointRight;
	private Vector3 aimStartHoldPointLeft;
	private bool holdAimStartRightUntilInput = false;
	private bool holdAimStartLeftUntilInput = false;
	private Collider[] breakoutAimAssistColliders;
	private readonly List<BodyController> breakoutAimAssistBodies = new List<BodyController>();
	private GameObject breakoutAimAssistDebugLeftVolume;
	private GameObject breakoutAimAssistDebugRightVolume;
	private Material breakoutAimAssistDebugLeftMaterial;
	private Material breakoutAimAssistDebugRightMaterial;
	[Header("Aim Swap Blend")]
	public float aimSwapDuration = 0.1f;
	public AnimationCurve aimSwapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	private bool isAimSwapInProgress = false;
	private float aimSwapElapsed = 0f;
	private Vector3 aimSwapStartWeights;
	private Vector3 aimSwapTargetWeights;
	private bool bulletTimeTriggerPending = false;
	private bool bulletTimeTriggeredForAimSwap = false;
	[Header("Aim Yaw Clamp")]
	public float aimYawLimit = 90f;
	public float aimYawFollowSpeedMin = 60f;
	public float aimYawFollowSpeedMax = 360f;
	public float aimYawInputForMaxSpeed = 0.5f;
	public AnimationCurve aimYawFollowCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
	private Vector2 lastHeadRotation;
	[Header("Standby Timing")]
	public float standbyReapplyDelay = 0.05f;
	private float standbyDelayTimer = 0f;
	private bool deferStandbyRight = false;
	private bool deferStandbyLeft = false;
	[SerializeField] private bool useStandbyPoseWhileMoving = true;
	private bool movementStandbyVisualActive = false;
	private bool hasMovementStandbyStoredAimRight = false;
	private bool hasMovementStandbyStoredAimLeft = false;
	private Vector3 movementStandbyStoredAimRight;
	private Vector3 movementStandbyStoredAimLeft;
	private Transform headAimTargetProxyRight;
	private Transform headAimTargetProxyLeft;
	private bool headAimUsesProxyTargets = false;
	[Header("Aim Scroll")]
	public float aimScrollToggleCooldown = 0.12f;
	private float nextAimScrollToggleTime = 0f;
	private bool freezeAimPointRight = false;
	private bool freezeAimPointLeft = false;
	private Vector3 frozenAimPointRight;
	private Vector3 frozenAimPointLeft;
	private bool releaseFrozenAimPointsOnSwapComplete = false;
	public Transform weaponAimPoint;
	public Transform weaponAimPointL;
	public Transform weaponStandbyPointR;
	public Transform weaponStandbyPointL;
	[SerializeField] private GameObject rightAimPointIndicator;
	[SerializeField] private GameObject leftAimPointIndicator;
	private bool rightAimPointIndicatorVisible = true;
	private bool leftAimPointIndicatorVisible = true;
	[Header("Standby Elbow Targets")]
	public Transform elbowTargetR;
	public Transform elbowTargetL;
	public float standbyElbowDrop = 0.35f;
	public float standbyElbowOut = 0.18f;
	public float standbyElbowBack = 0.08f;
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
	[SerializeField] private float knockbackSettleVelocityThreshold = 0.05f;
	[SerializeField] private float knockbackSettleDuration = 0.15f;
	[SerializeField] private float knockbackNavMeshSampleRadius = 1.0f;
	[SerializeField] private float knockbackStumblePerpendicularForceMultiplier = 0.2f;
	private float knockbackSettledTimer;
	[SerializeField] private bool enableAiBodyDriftCorrection = true;
	[SerializeField] private float bodyDriftCorrectionThreshold = 0.15f;
	[SerializeField] private float bodyDriftSnapThreshold = 0.6f;
	[SerializeField] private float bodyDriftCorrectionSpeed = 12f;
	[SerializeField] private bool logBodyDriftCorrection = false;

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
			aiHealth = Random.Range(2, 25);
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
		ResolveAimPointIndicators();
		UpdateAimPointIndicatorVisibility();
		SetupHeadAimTargetProxies();
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
		bool wasAlive = !isDead;
		bool wasShotByPlayer = i != null && i.sourceBodyController != null && !i.sourceBodyController.isAI;
		AuraManager sourceAuraManager = GetPlayerSourceAuraManager(i);
		bool shouldAwardPlayerAura = wasAlive && isAI && sourceAuraManager != null;

		if (shouldAwardPlayerAura)
		{
			sourceAuraManager.AddAuraTenths(PlayerDamageAuraRewardTenths);
		}

		legs.HandleTagging(i.limb, i.impactForce);
		weapons.HandleDisruption(i.limb);
		ApplyKnockback(i.impactVector, i.limb);
		// if (cooling.isOverheated)
		// {
		DamageSystem(i);
		//}

		if (bodyState != null && bodyState.hitStunAmount > 0f)
		{
			bodyState.RestartHitStunDecayDelay();
		}

		if (isAI && wasShotByPlayer && bodyState != null)
		{
			bodyState.NotifyShotByPlayer();
		}

		if (shouldAwardPlayerAura && isDead)
		{
			sourceAuraManager.AddAuraTenths(PlayerKillAuraRewardTenths);
		}

		//heatContainer.IncreaseHeat(this, i.amount);
		//cooling.IncreaseHeat(this, i.amount);
	}

	private AuraManager GetPlayerSourceAuraManager(DamageInfo i)
	{
		if (i == null || i.sourceBodyController == null || i.sourceBodyController.isAI)
		{
			return null;
		}

		if (i.sourceBodyController.auraManager == null)
		{
			i.sourceBodyController.auraManager = i.sourceBodyController.GetComponent<AuraManager>();
		}

		return i.sourceBodyController.auraManager;
	}

	public void DamageSystem(DamageInfo i)
	{
		if (i.limb.specificLimb == Limb.LimbID.none)
		{
			head.DamageHealth(i.amount * 0.5f);
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
					head.DamageHealth(i.amount * 0.5f);
					Mathf.Clamp01(bodyState.hitStunAmount += 1.5f);
					checkForRepair(i);
					break;
				case LimbID.rightLeg:
					legs.damangeRightLegCurrentHealth(i.amount);
					head.DamageHealth(i.amount * 0.5f);
					Mathf.Clamp01(bodyState.hitStunAmount += 1.5f);
					checkForRepair(i);
					break;
				case LimbID.torso:
					head.DamageHealth(i.amount);
					// Debug.Log(LimbID.torso + " " + i.amount + " " + head.currentHealth);
					Mathf.Clamp01(bodyState.hitStunAmount += 0.3f);
					break;
				case LimbID.head:
					head.DamageHealth(i.amount * 2);
					// Debug.Log(LimbID.head + " " + i.amount * 2 + " " + head.currentHealth);
					//head.Damage((int)i.amount);
					Mathf.Clamp01(bodyState.hitStunAmount += 0.5f);
					break;
				case LimbID.rightArm:
					head.DamageHealth(i.amount * 0.5f);
					Mathf.Clamp01(bodyState.hitStunAmount += 0.7f);
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

	public void DieFacingIncomingDirection(Vector3 incomingDirection)
	{
		FaceIncomingDirectionYaw(incomingDirection);
		Die();
	}

	private void FaceIncomingDirectionYaw(Vector3 incomingDirection)
	{
		if (isAI)
		{
			return;
		}

		incomingDirection.y = 0f;
		if (incomingDirection.sqrMagnitude <= 0.0001f)
		{
			return;
		}

		Quaternion targetRotation = Quaternion.LookRotation(incomingDirection.normalized, Vector3.up);
		if (rb != null && rb.transform == transform)
		{
			rb.rotation = targetRotation;
		}
		else
		{
			transform.rotation = targetRotation;
		}
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

	public void FireWeapon1(bool triggerPressedThisFrame = false)
	{
		if (!isAI && IsPlayerCenteredAim())
		{
			weapons.ExecuteWeapon1(true, triggerPressedThisFrame);
			weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
			weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
			weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
			return;
		}

		if (isAI || (!PrimaryAimUsesLeft && isAimingRight))
		{
			weapons.ExecuteWeapon1(true, triggerPressedThisFrame);

		}
		else if (PrimaryAimUsesLeft && isAimingLeft)
		{
			weapons.ExecuteWeapon1(false, triggerPressedThisFrame);
		}
		//Debug.Log(guns.ActiveGun1.Model.transform.position);

		// TODO This is just to debug the AI cycling power allocations
		weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
		weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
		weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
	}

	public void FireOffhandWeapon1(bool triggerPressedThisFrame = false)
	{
		if (!isAI && IsPlayerCenteredAim())
		{
			weapons.ExecuteWeapon1(false, triggerPressedThisFrame);
			weapon1gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[0]);
			weapon2gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[1]);
			weapon3gauge.SetActive(weapons.GetCurrentPowerAllocationDictionary()[2]);
			return;
		}

		bool primaryArmIsAiming = PrimaryAimUsesLeft ? isAimingLeft : isAimingRight;
		if (!primaryArmIsAiming)
		{
			return;
		}

		weapons.ExecuteWeapon1(PrimaryAimUsesLeft, triggerPressedThisFrame);

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
		Vector2 headRot = fixedTickHeadRotation;
		if ((IsAimSourceRight() && holdAimStartRightUntilInput)
			|| (IsAimSourceLeft() && holdAimStartLeftUntilInput))
		{
			// The assisted direction is the manual-aim baseline. Do not let moving-yaw
			// smoothing or head input drift away from it before the handoff occurs.
			lastHeadRotation = Vector2.zero;
			smoothedMovingAimYaw = 0f;
			return;
		}
		lastHeadRotation = headRot;
		bool isArmAiming = isAimingRight || isAimingLeft;
		if (isArmAiming || keepCameraAimWithoutArm)
		{
			float speedSqr = rb != null ? rb.velocity.sqrMagnitude : 0f;
			float moveStartThresholdSqr = torsoYawFollowsMouseMoveThreshold * torsoYawFollowsMouseMoveThreshold;
			float moveStopThresholdSqr = torsoYawFollowsMouseStopThreshold * torsoYawFollowsMouseStopThreshold;
			if (movingAimYawActive)
			{
				if (speedSqr < moveStopThresholdSqr)
				{
					movingAimYawActive = false;
				}
			}
			else if (speedSqr > moveStartThresholdSqr)
			{
				movingAimYawActive = true;
			}

			if (movingAimYawActive)
			{
				// While moving + aiming, feed mouse yaw into torso yaw for smooth look.
				float smoothing = Mathf.Max(0f, torsoYawFollowSmoothing);
				float t = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.deltaTime);
				smoothedMovingAimYaw = Mathf.Lerp(smoothedMovingAimYaw, headRot.y, t);
				transform.Rotate(0f, smoothedMovingAimYaw, 0f);
				sensors.setHeadRotation(new Vector2(headRot.x, 0f));
			}
			else
			{
				smoothedMovingAimYaw = 0f;
				ApplyAimYawClamp(ref headRot);
				// Standing still while aiming keeps existing head-only yaw behavior.
				sensors.setHeadRotation(headRot);
			}
		}
		else
		{
			movingAimYawActive = false;
			smoothedMovingAimYaw = 0f;
			// if (input.getHeadRotation().magnitude > 0)
			// {
			// 	cameraMoveScript.enabled = true;
			// }
			// Default: rotate both body and head
			sensors.setHeadRotation(new Vector2(headRot.x, 0));
			transform.Rotate(0, headRot.y, 0);
		}
	}

	void ToggleAimingRight()
	{
		if (isAimingRight)
		{
			RecenterRightArm();
			return;
		}

		FocusRightArm();
	}

	void ToggleAimingLeft()
	{
		if (isAimingLeft)
		{
			RecenterLeftArm();
			return;
		}

		FocusLeftArm();
	}

	private void FocusRightArm()
	{
		if (isAimingLeft)
		{
			CaptureRelativeAimLeft();
		}

		isAimingLeft = false;
		isAimingRight = true;
		keepCameraAimWithoutArm = false;
		keepCameraAimUsesLeft = false;
		aimStartHoldTimerLeft = 0f;
		holdAimStartLeftUntilInput = false;
		forceAimToTorsoLeft = false;

		bool wasBrokenOut = startedAimingRight;
		bool restoringParkedAim = wasBrokenOut || hasStoredRelativeAimRight;
		bool shouldTriggerBulletTime = false;
		if (!startedAimingRight)
		{
			startedAimingRight = true;
		}

		if (!wasBrokenOut && !hasStoredRelativeAimRight)
		{
			forceAimToTorsoRight = true;
			aimStartHoldPointRight = GetBreakoutStartAimPoint(false, out bool foundBreakoutAimAssistTarget, out _);
			shouldTriggerBulletTime = foundBreakoutAimAssistTarget;
			BeginAimStartHold(false, aimStartHoldPointRight);
		}
		else
		{
			forceAimToTorsoRight = false;
			aimStartHoldTimerRight = 0f;
			holdAimStartRightUntilInput = false;
			if (TryGetAssistedBreakoutStartAimPoint(false, out Vector3 assistedAimPoint, out bool foundBreakoutAimAssistTarget))
			{
				aimStartHoldPointRight = assistedAimPoint;
				BeginAimStartHold(false, aimStartHoldPointRight);
			}
			else
			{
				ApplyRelativeAimRight();
			}
			shouldTriggerBulletTime = foundBreakoutAimAssistTarget;
			if (restoringParkedAim)
			{
				AlignHeadAnchorToAimPoint(false);
			}
		}

		StartAimSwapBlend(0f, 1f, startedAimingLeft ? 0.7f : 0f);
		if (shouldTriggerBulletTime)
		{
			QueueBulletTimeTriggerForAimSwap();
		}
	}

	private void FocusLeftArm()
	{
		if (isAimingRight)
		{
			CaptureRelativeAimRight();
		}

		isAimingRight = false;
		isAimingLeft = true;
		keepCameraAimWithoutArm = false;
		keepCameraAimUsesLeft = true;
		aimStartHoldTimerRight = 0f;
		holdAimStartRightUntilInput = false;
		forceAimToTorsoRight = false;

		bool wasBrokenOut = startedAimingLeft;
		bool restoringParkedAim = wasBrokenOut || hasStoredRelativeAimLeft;
		bool shouldTriggerBulletTime = false;
		if (!startedAimingLeft)
		{
			startedAimingLeft = true;
		}

		if (!wasBrokenOut && !hasStoredRelativeAimLeft)
		{
			forceAimToTorsoLeft = true;
			if (headObjectL != null && headObjectTransformCache != null)
			{
				headObjectL.transform.SetPositionAndRotation(
					headObjectTransformCache.position,
					GetTorsoYawPitchRotation());
			}
			aimStartHoldPointLeft = GetBreakoutStartAimPoint(true, out bool foundBreakoutAimAssistTarget, out _);
			shouldTriggerBulletTime = foundBreakoutAimAssistTarget;
			BeginAimStartHold(true, aimStartHoldPointLeft);
		}
		else
		{
			forceAimToTorsoLeft = false;
			aimStartHoldTimerLeft = 0f;
			holdAimStartLeftUntilInput = false;
			if (TryGetAssistedBreakoutStartAimPoint(true, out Vector3 assistedAimPoint, out bool foundBreakoutAimAssistTarget))
			{
				aimStartHoldPointLeft = assistedAimPoint;
				BeginAimStartHold(true, aimStartHoldPointLeft);
			}
			else
			{
				ApplyRelativeAimLeft();
			}
			shouldTriggerBulletTime = foundBreakoutAimAssistTarget;
			if (restoringParkedAim)
			{
				AlignHeadAnchorToAimPoint(true);
			}
		}

		StartAimSwapBlend(0f, startedAimingRight ? 0.7f : 0f, 1f);
		if (shouldTriggerBulletTime)
		{
			QueueBulletTimeTriggerForAimSwap();
		}
	}

	private void RecenterRightArm()
	{
		isAimingRight = false;
		startedAimingRight = false;
		hasStoredRelativeAimRight = false;
		useStoredAimRight = false;
		forceAimToTorsoRight = false;
		aimStartHoldTimerRight = 0f;
		holdAimStartRightUntilInput = false;

		EnterCenteredState();
	}

	private void ParkRightArmInCentered()
	{
		CaptureRelativeAimRight();
		isAimingRight = false;
		startedAimingRight = true;
		forceAimToTorsoRight = false;
		aimStartHoldTimerRight = 0f;
		holdAimStartRightUntilInput = false;
		EnterCenteredState();
	}

	private void RecenterLeftArm()
	{
		isAimingLeft = false;
		startedAimingLeft = false;
		hasStoredRelativeAimLeft = false;
		useStoredAimLeft = false;
		forceAimToTorsoLeft = false;
		aimStartHoldTimerLeft = 0f;
		holdAimStartLeftUntilInput = false;

		EnterCenteredState();
	}

	private void ParkLeftArmInCentered()
	{
		CaptureRelativeAimLeft();
		isAimingLeft = false;
		startedAimingLeft = true;
		forceAimToTorsoLeft = false;
		aimStartHoldTimerLeft = 0f;
		holdAimStartLeftUntilInput = false;
		EnterCenteredState();
	}

	private void EnterCenteredState()
	{
		isAimingRight = false;
		isAimingLeft = false;
		keepCameraAimWithoutArm = false;
		keepCameraAimUsesLeft = false;
		bulletTimeTriggerPending = false;
		bulletTimeTriggeredForAimSwap = false;
		if (!startedAimingRight)
		{
			hasStoredRelativeAimRight = false;
		}
		if (!startedAimingLeft)
		{
			hasStoredRelativeAimLeft = false;
		}
		useStoredAimRight = false;
		useStoredAimLeft = false;
		forceAimToTorsoRight = false;
		forceAimToTorsoLeft = false;
		aimStartHoldTimerRight = 0f;
		aimStartHoldTimerLeft = 0f;
		holdAimStartRightUntilInput = false;
		holdAimStartLeftUntilInput = false;
		StartAimSwapBlend(1f, 0f, 0f);
	}

	private void GetAimPoint()
	{
		if (Time.time - lastRaycastTime >= raycastInterval)
		{
			Vector3 origin = GetAimLockOrigin();
			Vector3 centeredForward = GetTorsoYawPitchRotation() * Vector3.forward;
			Vector3 centeredFallback = origin + centeredForward * 20f;
			Vector3 centeredTarget = ResolveAimPoint(origin, centeredForward, centeredFallback);
			bool aimSourceRight = IsAimSourceRight();
			bool aimSourceLeft = IsAimSourceLeft();

			if (IsPlayerCenteredAim())
			{
				if (startedAimingRight)
				{
					ApplyRelativeAimRight();
				}
				else
				{
					SetWeaponAimPointR(centeredTarget);
				}

				if (startedAimingLeft)
				{
					ApplyRelativeAimLeft();
				}
				else
				{
					SetWeaponAimPointL(centeredTarget);
				}

				torsoAimPoint.position = centeredTarget;
				if (!freezeHeadDuringMoveAimYaw)
				{
					headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
					headObjectL.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
				}
				return;
			}

			if (aimSourceRight)
			{
				if (aimStartHoldTimerRight > 0f)
				{
					aimStartHoldTimerRight -= Time.deltaTime;
					MaintainAimStartHoldAlignment(false);
					torsoAimPoint.position = centeredTarget;
					return;
				}

				if (holdAimStartRightUntilInput)
				{
					if (!HasAimStartReleaseInput())
					{
						MaintainAimStartHoldAlignment(false);
						torsoAimPoint.position = centeredTarget;
						return;
					}

					ReleaseAimStartHold(false);
					torsoAimPoint.position = centeredTarget;
					return;
				}
			}

			if (aimSourceLeft)
			{
				if (aimStartHoldTimerLeft > 0f)
				{
					aimStartHoldTimerLeft -= Time.deltaTime;
					MaintainAimStartHoldAlignment(true);
					torsoAimPoint.position = centeredTarget;
					return;
				}

				if (holdAimStartLeftUntilInput)
				{
					if (!HasAimStartReleaseInput())
					{
						MaintainAimStartHoldAlignment(true);
						torsoAimPoint.position = centeredTarget;
						return;
					}

					ReleaseAimStartHold(true);
					torsoAimPoint.position = centeredTarget;
					return;
				}
			}

			if (freezeHeadDuringMoveAimYaw)
			{
				torsoAimPoint.position = centeredTarget;
				return;
			}

			if (!aimSourceRight)
			{
				if (startedAimingRight)
				{
					ApplyRelativeAimRight();
				}
				else
				{
					SetWeaponAimPointR(centeredTarget);
				}
			}

			if (!aimSourceLeft)
			{
				if (startedAimingLeft)
				{
					ApplyRelativeAimLeft();
				}
				else
				{
					SetWeaponAimPointL(centeredTarget);
				}
			}

			if (!aimSourceRight && !aimSourceLeft)
			{
				torsoAimPoint.position = centeredTarget;
				return;
			}

			if (aimSourceRight && forceAimToTorsoRight)
			{
				SetWeaponAimPointR(aimStartHoldPointRight);
				torsoAimPoint.position = centeredTarget;
				forceAimToTorsoRight = false;
				return;
			}
			if (aimSourceLeft && forceAimToTorsoLeft)
			{
				SetWeaponAimPointL(aimStartHoldPointLeft);
				torsoAimPoint.position = centeredTarget;
				forceAimToTorsoLeft = false;
				return;
			}

			Quaternion cameraRot = Quaternion.Euler(aimCam.transform.eulerAngles.x, aimCam.transform.eulerAngles.y, 0f);
			Vector3 rayForward = cameraRot * Vector3.forward;
			Vector3 cameraFallback = origin + rayForward * 20f;
			Vector3 cameraTarget = ResolveAimPoint(origin, rayForward, cameraFallback);
			if (aimSourceRight)
			{
				SetWeaponAimPointR(cameraTarget);
			}
			else if (aimSourceLeft)
			{
				SetWeaponAimPointL(cameraTarget);
			}
			torsoAimPoint.position = cameraTarget;
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

	private Vector3 ResolveAimPoint(Vector3 origin, Vector3 forward, Vector3 fallback)
	{
		if (forward.sqrMagnitude <= 0.0001f)
		{
			return fallback;
		}

		Ray ray = new Ray(origin, forward.normalized);
		RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, aimMask);
		if (hits.Length <= 0)
		{
			return fallback;
		}

		RaycastHit? nearestValidHit = null;
		foreach (var hit in hits)
		{
			bool isOwnCollider = false;
			if (bodyColliders != null)
			{
				foreach (var collider in bodyColliders)
				{
					if (hit.collider == collider)
					{
						isOwnCollider = true;
						break;
					}
				}
			}

			if (isOwnCollider)
			{
				continue;
			}

			int hitLayer = hit.collider.gameObject.layer;
			if (hitLayer != 6 && hitLayer != 9)
			{
				continue;
			}

			if (!nearestValidHit.HasValue || hit.distance < nearestValidHit.Value.distance)
			{
				nearestValidHit = hit;
			}
		}

		return nearestValidHit.HasValue ? nearestValidHit.Value.point : fallback;
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

		SetWeaponAimPointR(torso);
		SetWeaponAimPointL(torso);
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

	private Vector3 GetAimLockOrigin()
	{
		return headObjectTransformCache != null ? headObjectTransformCache.position : transform.position;
	}

	private void SetTorsoAimPointToCurrentView()
	{
		if (torsoAimPoint == null || headObjectTransformCache == null || aimCam == null)
		{
			return;
		}

		torsoAimPoint.position = headObjectTransformCache.position + (aimCam.transform.forward * 20f);
	}

	private void CaptureRelativeAimRight()
	{
		if (weaponAimPoint == null)
		{
			return;
		}

		Vector3 origin = GetAimLockOrigin();
		Quaternion invTorsoYaw = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
		Vector3 aimPosition = movementStandbyVisualActive && hasMovementStandbyStoredAimRight
			? movementStandbyStoredAimRight
			: weaponAimPoint.position;
		storedRelativeAimRightLocal = invTorsoYaw * (aimPosition - origin);
		if (storedRelativeAimRightLocal.sqrMagnitude < 0.0001f)
		{
			storedRelativeAimRightLocal = Vector3.forward * 20f;
		}
		hasStoredRelativeAimRight = true;
	}

	private void CaptureRelativeAimLeft()
	{
		if (weaponAimPointL == null)
		{
			return;
		}

		Vector3 origin = GetAimLockOrigin();
		Quaternion invTorsoYaw = Quaternion.Inverse(Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
		Vector3 aimPosition = movementStandbyVisualActive && hasMovementStandbyStoredAimLeft
			? movementStandbyStoredAimLeft
			: weaponAimPointL.position;
		storedRelativeAimLeftLocal = invTorsoYaw * (aimPosition - origin);
		if (storedRelativeAimLeftLocal.sqrMagnitude < 0.0001f)
		{
			storedRelativeAimLeftLocal = Vector3.forward * 20f;
		}
		hasStoredRelativeAimLeft = true;
	}

	private void ApplyRelativeAimRight()
	{
		if (!hasStoredRelativeAimRight)
		{
			CaptureRelativeAimRight();
		}

		Vector3 origin = GetAimLockOrigin();
		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
		SetWeaponAimPointR(origin + (torsoYaw * storedRelativeAimRightLocal));
	}

	private void ApplyRelativeAimLeft()
	{
		if (!hasStoredRelativeAimLeft)
		{
			CaptureRelativeAimLeft();
		}

		Vector3 origin = GetAimLockOrigin();
		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
		SetWeaponAimPointL(origin + (torsoYaw * storedRelativeAimLeftLocal));
	}

	private void BeginAimStartHold(bool useLeft, Vector3 aimPoint)
	{
		if (useLeft)
		{
			aimStartHoldPointLeft = aimPoint;
			aimStartHoldTimerLeft = aimStartHoldDuration;
			holdAimStartLeftUntilInput = true;
		}
		else
		{
			aimStartHoldPointRight = aimPoint;
			aimStartHoldTimerRight = aimStartHoldDuration;
			holdAimStartRightUntilInput = true;
		}

		smoothedMovingAimYaw = 0f;
		MaintainAimStartHoldAlignment(useLeft);
		ApplyCameraImmediateForAimSwap();
	}

	private void MaintainAimStartHoldAlignment(bool useLeft)
	{
		Vector3 aimPoint = useLeft ? aimStartHoldPointLeft : aimStartHoldPointRight;
		if (useLeft)
		{
			SetWeaponAimPointL(aimPoint);
		}
		else
		{
			SetWeaponAimPointR(aimPoint);
		}

		AlignHeadAnchorToAimPoint(useLeft);
		if (sensors != null)
		{
			sensors.SyncHeadPitchFromCurrentTransform(useLeft);
		}
	}

	private bool HasAimStartReleaseInput()
	{
		float deadzone = Mathf.Max(0f, aimStartReleaseInputDeadzone);
		return fixedTickHeadRotation.sqrMagnitude > deadzone * deadzone;
	}

	private void ReleaseAimStartHold(bool useLeft)
	{
		// Rebase the manual controller and camera to the assisted direction before
		// the sampled mouse delta is applied later in this FixedUpdate.
		MaintainAimStartHoldAlignment(useLeft);
		ApplyCameraImmediateForAimSwap();
		smoothedMovingAimYaw = 0f;

		if (useLeft)
		{
			holdAimStartLeftUntilInput = false;
			forceAimToTorsoLeft = false;
		}
		else
		{
			holdAimStartRightUntilInput = false;
			forceAimToTorsoRight = false;
		}
	}

	private void AlignHeadAnchorToAimPoint(bool useLeft)
	{
		Transform head = useLeft
			? (headObjectL != null ? headObjectL.transform : null)
			: (headObject != null ? headObject.transform : null);
		Transform aimPoint = useLeft ? weaponAimPointL : weaponAimPoint;
		if (head == null || aimPoint == null)
		{
			return;
		}

		Vector3 origin = headObjectTransformCache != null ? headObjectTransformCache.position : head.position;
		Vector3 direction = aimPoint.position - origin;
		if (direction.sqrMagnitude < 0.0001f)
		{
			return;
		}

		head.SetPositionAndRotation(origin, Quaternion.LookRotation(direction.normalized, Vector3.up));
	}

	private void SetupHeadAimTargetProxies()
	{
		if (isAI || headAimConstraint == null || weaponAimPoint == null || weaponAimPointL == null)
		{
			return;
		}

		var sources = headAimConstraint.data.sourceObjects;
		if (sources.Count < 3)
		{
			return;
		}

		headAimTargetProxyRight = CreateHeadAimTargetProxy("HeadAimTargetProxy_R", weaponAimPoint.position);
		headAimTargetProxyLeft = CreateHeadAimTargetProxy("HeadAimTargetProxy_L", weaponAimPointL.position);

		var rightSource = sources[1];
		var leftSource = sources[2];
		rightSource.transform = headAimTargetProxyRight;
		leftSource.transform = headAimTargetProxyLeft;
		sources[1] = rightSource;
		sources[2] = leftSource;
		headAimConstraint.data.sourceObjects = sources;
		headAimUsesProxyTargets = true;

		RigBuilder rigBuilder = GetComponentInParent<RigBuilder>();
		if (rigBuilder == null)
		{
			rigBuilder = GetComponentInChildren<RigBuilder>();
		}
		if (rigBuilder != null && rigBuilder.isActiveAndEnabled)
		{
			rigBuilder.Build();
		}
		SyncHeadAimTargetProxies();
	}

	private Transform CreateHeadAimTargetProxy(string proxyName, Vector3 position)
	{
		GameObject proxy = new GameObject(proxyName);
		proxy.transform.SetParent(transform, true);
		proxy.transform.position = position;
		proxy.transform.rotation = transform.rotation;
		return proxy.transform;
	}

	private void SyncHeadAimTargetProxies()
	{
		if (!headAimUsesProxyTargets)
		{
			return;
		}

		if (headAimTargetProxyRight != null)
		{
			headAimTargetProxyRight.position = GetHeadAimTargetProxyPosition(false);
		}
		if (headAimTargetProxyLeft != null)
		{
			headAimTargetProxyLeft.position = GetHeadAimTargetProxyPosition(true);
		}
	}

	private Vector3 GetHeadAimTargetProxyPosition(bool useLeft)
	{
		if (movementStandbyVisualActive)
		{
			if (useLeft && isAimingLeft && headObjectAimOffsetL != null)
			{
				return headObjectAimOffsetL.position;
			}
			if (!useLeft && isAimingRight && headObjectAimOffset != null)
			{
				return headObjectAimOffset.position;
			}
		}

		Transform aimPoint = useLeft ? weaponAimPointL : weaponAimPoint;
		return aimPoint != null ? aimPoint.position : transform.position;
	}

	#endregion

	private void ProcessAimScrollInput(bool scrollUp, bool scrollDown, bool isAIControls)
	{
		if (Time.time < nextAimScrollToggleTime)
		{
			return;
		}

		if (scrollUp)
		{
			if (isAIControls) HandleScrollUpAI();
			else HandleScrollUp();
			nextAimScrollToggleTime = Time.time + Mathf.Max(0f, aimScrollToggleCooldown);
		}
		else if (scrollDown)
		{
			if (isAIControls) HandleScrollDownAI();
			else HandleScrollDown();
			nextAimScrollToggleTime = Time.time + Mathf.Max(0f, aimScrollToggleCooldown);
		}
	}

	private void SwitchToLoweredSide(bool useLeftSide)
	{
		EnterCenteredState();
	}

	private void SwitchToLoweredSideAI(bool useLeftSide)
	{
		bool wasAimingRight = isAimingRight;
		bool wasAimingLeft = isAimingLeft;

		if (isAimingRight)
		{
			CaptureRelativeAimRight();
			isAimingRight = false;
		}
		if (isAimingLeft)
		{
			CaptureRelativeAimLeft();
			isAimingLeft = false;
		}

		keepCameraAimWithoutArm = true;
		keepCameraAimUsesLeft = useLeftSide;
		SetTorsoAimPointToCurrentView();
		if (wasAimingRight || wasAimingLeft)
		{
			StartAimSwapBlendAI(1f, 0f, 0f);
		}
		else
		{
			SetAimStandbyImmediate();
		}
	}

	private bool IsLoweredSideSelected(bool useLeftSide)
	{
		return keepCameraAimWithoutArm
			&& !isAimingRight
			&& !isAimingLeft
			&& keepCameraAimUsesLeft == useLeftSide;
	}

	private bool IsPlayerCenteredAim()
	{
		return !isAI
			&& !offhandMirrorActive
			&& !isAimingRight
			&& !isAimingLeft
			&& !keepCameraAimWithoutArm;
	}

	private bool IsAimSourceRight()
	{
		return isAimingRight && (!offhandMirrorActive || !offhandMirrorSourceIsLeft);
	}

	private bool IsAimSourceLeft()
	{
		return isAimingLeft && (!offhandMirrorActive || offhandMirrorSourceIsLeft);
	}

	private bool IsActiveArmLeft()
	{
		if (isAimingLeft && !isAimingRight)
		{
			return true;
		}

		if (isAimingRight && !isAimingLeft)
		{
			return false;
		}

		// Fallback for standby or transient mixed states: use selected side.
		return keepCameraAimUsesLeft;
	}

	private bool IsActiveArmAiming()
	{
		bool activeArmIsLeft = IsActiveArmLeft();
		return activeArmIsLeft ? isAimingLeft : isAimingRight;
	}

	private bool UpdateOffhandMirrorAimInput(bool scrollUp, bool scrollDown, bool aimMiddle)
	{
		if (isAI || input == null)
		{
			return false;
		}

		bool shiftHeld = input.getShift();
		bool scrollRequested = scrollUp || scrollDown;
		if (!shiftHeld)
		{
			if (offhandMirrorActive)
			{
				EndOffhandMirrorAim(offhandMirrorRestoreOffhandOnRelease);
			}
			return false;
		}

		if (!offhandMirrorActive && (scrollRequested || !aimMiddle))
		{
			TryBeginOffhandMirrorAim();
		}

		if (offhandMirrorActive && scrollRequested)
		{
			HandleOffhandMirrorScroll(scrollUp);
			return true;
		}

		if (offhandMirrorActive && aimMiddle)
		{
			EndOffhandMirrorAim(true);
		}

		return false;
	}

	private void TryBeginOffhandMirrorAim()
	{
		if (offhandMirrorActive)
		{
			return;
		}

		bool hasSingleActiveAim = isAimingRight != isAimingLeft;
		bool hasLoweredSelectedSide = keepCameraAimWithoutArm && !isAimingRight && !isAimingLeft;
		if (!hasSingleActiveAim && !hasLoweredSelectedSide)
		{
			return;
		}

		offhandMirrorSourceIsLeft = hasSingleActiveAim ? isAimingLeft : keepCameraAimUsesLeft;
		offhandMirrorCameraUsesLeft = offhandMirrorSourceIsLeft;
		Transform offhandAimPoint = offhandMirrorSourceIsLeft ? weaponAimPoint : weaponAimPointL;
		if (offhandAimPoint == null)
		{
			return;
		}

		offhandMirrorStoredAimPoint = offhandAimPoint.position;
		offhandMirrorStoredAimingRight = isAimingRight;
		offhandMirrorStoredAimingLeft = isAimingLeft;
		offhandMirrorStoredStartedRight = startedAimingRight;
		offhandMirrorStoredStartedLeft = startedAimingLeft;
		offhandMirrorStoredKeepCameraAimWithoutArm = keepCameraAimWithoutArm;
		offhandMirrorStoredKeepCameraAimUsesLeft = keepCameraAimUsesLeft;
		offhandMirrorRestoreOffhandOnRelease = true;
		offhandMirrorActive = true;

		ApplyOffhandMirrorAimPoint();
	}

	private void HandleOffhandMirrorScroll(bool scrollUp)
	{
		bool targetUsesLeft = scrollUp;
		bool switchingToMirroredOffhand = (scrollUp && !offhandMirrorSourceIsLeft)
			|| (!scrollUp && offhandMirrorSourceIsLeft);
		bool sourceWasLowered = offhandMirrorStoredKeepCameraAimWithoutArm
			&& !offhandMirrorStoredAimingRight
			&& !offhandMirrorStoredAimingLeft;

		ApplyOffhandMirrorAimPoint();
		if (sourceWasLowered && switchingToMirroredOffhand)
		{
			EndOffhandMirrorAim(true);
			SwitchToLoweredSideFromOffhandMirror(targetUsesLeft);
			nextAimScrollToggleTime = Time.time + Mathf.Max(0f, aimScrollToggleCooldown);
			return;
		}

		if (switchingToMirroredOffhand)
		{
			if (offhandMirrorSourceIsLeft)
			{
				CaptureRelativeAimRight();
				startedAimingRight = true;
			}
			else
			{
				CaptureRelativeAimLeft();
				startedAimingLeft = true;
			}
		}
		offhandMirrorStoredStartedRight = startedAimingRight;
		offhandMirrorStoredStartedLeft = startedAimingLeft;
		offhandMirrorRestoreOffhandOnRelease = false;
		EndOffhandMirrorAim(!switchingToMirroredOffhand);

		if (scrollUp)
		{
			HandleScrollUp();
		}
		else
		{
			HandleScrollDown();
		}

		nextAimScrollToggleTime = Time.time + Mathf.Max(0f, aimScrollToggleCooldown);
	}

	private void SwitchToLoweredSideFromOffhandMirror(bool useLeftSide)
	{
		if (useLeftSide)
		{
			startedAimingLeft = false;
			aimStartHoldTimerLeft = 0f;
			holdAimStartLeftUntilInput = false;
		}
		else
		{
			startedAimingRight = false;
			aimStartHoldTimerRight = 0f;
			holdAimStartRightUntilInput = false;
		}

		SwitchToLoweredSide(useLeftSide);
	}

	private void EndOffhandMirrorAim(bool restoreOffhandTarget)
	{
		if (!offhandMirrorActive)
		{
			return;
		}

		if (!restoreOffhandTarget)
		{
			ApplyOffhandMirrorAimPoint();
		}

		if (restoreOffhandTarget && offhandMirrorSourceIsLeft)
		{
			SetWeaponAimPointR(offhandMirrorStoredAimPoint);
		}
		else if (restoreOffhandTarget)
		{
			SetWeaponAimPointL(offhandMirrorStoredAimPoint);
		}

		isAimingRight = offhandMirrorStoredAimingRight;
		isAimingLeft = offhandMirrorStoredAimingLeft;
		startedAimingRight = offhandMirrorStoredStartedRight;
		startedAimingLeft = offhandMirrorStoredStartedLeft;
		keepCameraAimWithoutArm = offhandMirrorStoredKeepCameraAimWithoutArm;
		keepCameraAimUsesLeft = offhandMirrorStoredKeepCameraAimUsesLeft;
		offhandMirrorActive = false;
	}

	private void ApplyOffhandMirrorAimPoint()
	{
		if (!offhandMirrorActive)
		{
			return;
		}

		Transform sourceAimPoint = offhandMirrorSourceIsLeft ? weaponAimPointL : weaponAimPoint;
		if (sourceAimPoint == null)
		{
			return;
		}

		if (offhandMirrorSourceIsLeft)
		{
			SetWeaponAimPointR(sourceAimPoint.position);
		}
		else
		{
			SetWeaponAimPointL(sourceAimPoint.position);
		}
	}

	void HandleScrollUp()
	{
		if (isAimingLeft)
		{
			TriggerBulletTimeForVisibleBreakoutTargets(true);
			return;
		}

		if (isAimingRight)
		{
			if (startedAimingLeft)
			{
				FocusLeftArm();
				return;
			}

			FocusLeftArm();
			return;
		}

		FocusLeftArm();
	}

	void HandleScrollDown()
	{
		if (isAimingRight)
		{
			TriggerBulletTimeForVisibleBreakoutTargets(false);
			return;
		}

		if (isAimingLeft)
		{
			if (startedAimingRight)
			{
				FocusRightArm();
				return;
			}

			FocusRightArm();
			return;
		}

		FocusRightArm();
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
	private void ApplyKnockback(Vector3 force, Limb l)
	{
		isKnockbacked = true;
		knockbackSettledTimer = 0f;
		if (isAI)
		{
			if (agent == null)
			{
				agent = GetComponentInParent<NavMeshAgent>();
			}

			if (agent != null && agent.enabled)
			{
				agentDestination = agent.hasPath ? agent.destination : agent.transform.position;
				agent.isStopped = true;
				agent.ResetPath();
				agent.velocity = Vector3.zero;
			}
		}
		// if (agent != null)
		// {
		// 	agent.enabled = false;
		// }

		bool backTooCloseToWall = false;
		RaycastHit hit;
		if (Physics.Raycast(transform.position + KnockbackHeightCheck, -transform.forward, out hit, 2.0f, aimMask))
		{
			backTooCloseToWall = true;
		}

		float knockbackScale = !backTooCloseToWall ? 1f / 6f : 1f / 8f;
		Vector3 knockbackForce = force * knockbackScale * (1 - legs.getTagging()) * GetKnockbackFromLimb(l);
		knockbackForce += GetRandomPerpendicularKnockbackForce(knockbackForce);
		knockbackForce *= BulletTimeManager.GetScale(isAI ? BulletTimeChannel.EnemyHitReaction : BulletTimeChannel.PlayerActiveRagdoll);
		rb.AddForce(knockbackForce);
		knockbackTimer = minKnockbackDuration;
	}

	private Vector3 GetRandomPerpendicularKnockbackForce(Vector3 knockbackForce)
	{
		if (knockbackStumblePerpendicularForceMultiplier <= 0f || knockbackForce.sqrMagnitude <= 0.0001f)
		{
			return Vector3.zero;
		}

		Vector3 perpendicular = Vector3.Cross(knockbackForce.normalized, Vector3.up);
		if (perpendicular.sqrMagnitude <= 0.0001f)
		{
			perpendicular = Vector3.Cross(knockbackForce.normalized, Vector3.right);
		}

		return perpendicular.normalized
			* knockbackForce.magnitude
			* knockbackStumblePerpendicularForceMultiplier
			* Random.Range(-1f, 1f);
	}

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
		if (!isKnockbacked)
		{
			return;
		}

		if (knockbackTimer > 0f)
		{
			knockbackTimer -= Time.deltaTime;
			return;
		}

		if (rb == null)
		{
			FinishKnockbackRecovery(transform.position);
			return;
		}

		float velocityThresholdSqr = knockbackSettleVelocityThreshold * knockbackSettleVelocityThreshold;
		if (rb.velocity.sqrMagnitude > velocityThresholdSqr || Mathf.Abs(rb.velocity.y) > knockbackSettleVelocityThreshold)
		{
			knockbackSettledTimer = 0f;
			return;
		}

		knockbackSettledTimer += Time.deltaTime;
		if (knockbackSettledTimer < knockbackSettleDuration)
		{
			return;
		}

		FinishKnockbackRecovery(rb.transform.position);
	}

	private void FinishKnockbackRecovery(Vector3 bodyWorldPosition)
	{
		Vector3 recoveryPosition = bodyWorldPosition;
		bool recoveredToBody = !isAI;
		if (isAI)
		{
			if (agent == null)
			{
				agent = GetComponentInParent<NavMeshAgent>();
			}

			if (agent != null && agent.enabled)
			{
				bool warped = false;
				Vector3 sampleOrigin = new Vector3(bodyWorldPosition.x, agent.transform.position.y, bodyWorldPosition.z);
				if (NavMesh.SamplePosition(sampleOrigin, out NavMeshHit hit, knockbackNavMeshSampleRadius, agent.areaMask))
				{
					recoveryPosition = hit.position;
					warped = agent.Warp(recoveryPosition);
				}

				recoveredToBody = warped;

				agent.ResetPath();
				agent.isStopped = false;
			}
		}

		isKnockbacked = false;
		knockbackSettledTimer = 0f;
		if (rb != null)
		{
			rb.velocity = Vector3.zero;
			rb.angularVelocity = Vector3.zero;
			if (recoveredToBody)
			{
				rb.transform.localPosition = new Vector3(0f, rb.transform.localPosition.y, 0f);
			}
		}

		if (isAI && agent != null && agent.enabled)
		{
			AgentMoveBehavior moveBehavior = agent.GetComponent<AgentMoveBehavior>();
			if (moveBehavior != null)
			{
				moveBehavior.RefreshCurrentDestination();
			}
			else if (agentDestination != Vector3.zero)
			{
				agent.SetDestination(agentDestination);
			}
		}
	}

	private void CorrectAiBodyDrift()
	{
		if (!enableAiBodyDriftCorrection
			|| !isAI
			|| isDead
			|| isKnockbacked
			|| rb == null)
		{
			return;
		}

		if (agent == null)
		{
			agent = GetComponentInParent<NavMeshAgent>();
		}

		if (agent == null || !agent.enabled)
		{
			return;
		}

		Vector3 localPosition = rb.transform.localPosition;
		Vector2 horizontalDrift = new Vector2(localPosition.x, localPosition.z);
		float driftMagnitude = horizontalDrift.magnitude;
		if (driftMagnitude <= bodyDriftCorrectionThreshold)
		{
			return;
		}

		Vector3 correctedHorizontalPosition;
		bool snapped = driftMagnitude > bodyDriftSnapThreshold;
		if (snapped)
		{
			correctedHorizontalPosition = Vector3.zero;
		}
		else
		{
			Vector3 currentHorizontalPosition = new Vector3(localPosition.x, 0f, localPosition.z);
			correctedHorizontalPosition = Vector3.MoveTowards(
				currentHorizontalPosition,
				Vector3.zero,
				bodyDriftCorrectionSpeed * Time.fixedDeltaTime);
		}

		rb.transform.localPosition = new Vector3(correctedHorizontalPosition.x, localPosition.y, correctedHorizontalPosition.z);
		ClearRigidbodyLateralVelocity();

		if (logBodyDriftCorrection)
		{
			Debug.Log($"BodyController: corrected AI body drift for '{name}' from {driftMagnitude:F2}m using {(snapped ? "snap" : "smooth")} correction.");
		}
	}

	private void ClearRigidbodyLateralVelocity()
	{
		if (rb == null || agent == null)
		{
			return;
		}

		Vector3 localVelocity = agent.transform.InverseTransformDirection(rb.velocity);
		localVelocity.x = 0f;
		localVelocity.z = 0f;
		rb.velocity = agent.transform.TransformDirection(localVelocity);
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
		if (!isAI && IsPlayerCenteredAim())
		{
			Gun rightGun = guns != null ? guns.ActiveGun1 : null;
			Gun leftGun = gunsL != null ? gunsL.ActiveGun1 : null;
			bool reloadRight = rightGun != null && rightGun.CanStartReload;
			bool reloadLeft = leftGun != null && leftGun.CanStartReload;

			if (reloadRight && reloadLeft)
			{
				rightGun.StartReload();
				leftGun.StartReload(dualReloadAudioStaggerSeconds);
			}
			else if (reloadRight)
			{
				rightGun.StartReload();
			}
			else if (reloadLeft)
			{
				leftGun.StartReload();
			}
			return;
		}

		if (IsActiveArmLeft())
		{
			if (gunsL != null && gunsL.ActiveGun1 != null)
			{
				gunsL.ActiveGun1.StartReload();
			}
			return;
		}

		if (guns != null && guns.ActiveGun1 != null)
		{
			guns.ActiveGun1.StartReload();
		}
	}

	private void setJointStrength()
	{
		// float overheated = 1f;
		float ragdollScale = BulletTimeManager.GetScale(BulletTimeChannel.EnemyHitReaction);

		tempJoint = upperTorsoJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp((((1 - bodyState.hitStunAmount) * 100000) + 2000) * ragdollScale, 1000, 100000);
		upperTorsoJoint.slerpDrive = tempJoint;

		tempJoint = middleTorsoJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp((((1 - bodyState.hitStunAmount) * 100000) + 2000) * ragdollScale, 1000, 100000);
		middleTorsoJoint.slerpDrive = tempJoint;

		tempJoint = upperRightArmJoint.slerpDrive;
		tempJoint.positionSpring = Mathf.Clamp((((1 - bodyState.hitStunAmount) * 200000) + 2000) * ragdollScale, 1000, 100000);
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
		Vector3 headForward = PrimaryAimUsesLeft ? headObjectL.transform.forward : headObject.transform.forward;

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
		Vector3 headForward = PrimaryAimUsesLeft ? headObjectL.transform.forward : headObject.transform.forward;

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

	public float getAuraDamageMultiplier()
	{
		return auraManager != null ? auraManager.AuraFloat : 1f;
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
		UpdateBreakoutAimAssistDebugVolumes();
	}

	private void FixedUpdate()
	{
		if (!isDead)
		{
			if (isAI)
			{
				ExecutePhysicsBasedInputsAI();
				GetAimPointAI();
				DoRotationAI();
				UpdatePendingMoveAimYawAI();
				UpdateAimSwapBlendAI();
			}
			else
			{
				fixedTickHeadRotation = input != null ? input.getHeadRotation() : Vector2.zero;
				RestoreMovementStandbyVisualAimPoints();
				ExecutePhysicsBasedInputs();
				GetAimPoint();
				ApplyOffhandMirrorAimPoint();
				DoRotation();
				UpdatePendingMoveAimYaw();
				UpdateAimSwapBlend();
				ApplyMovementStandbyVisualPose();
				SyncHeadAimTargetProxies();
				UpdateStandbyElbowTargets();
			}
			doSiphoning();
			doLimbRepairs();
		}
		UpdateAimPointIndicatorVisibility();
		legs.DoMoveDeacceleration();
		legs.RecoverFromTagging(1);
		legs.UpdateMovementTick(Time.deltaTime);
		weapons.RecoverFromDisruption();
		// doCooling();
		HandleKnockback();
		CorrectAiBodyDrift();
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
		legs.speedMultiplier = BulletTimeManager.GetScale(BulletTimeChannel.PlayerMovement);

		if (legs.isCurrentVelocityLessThanMax())
		{
			if (input.getForward()) MoveForward();
			if (input.getBackward()) MoveBackward();
			if (input.getLeft()) MoveLeft();
			if (input.getRight()) MoveRight();
		}

		float maxSpeed = legs.baseWalkSpeed * legs.getMoveSpeed();
		float maxFireSpeed = maxSpeed * 0.7f;
		bool fire1Pressed = input.getFire1Down();
		bool fire2Pressed = input.getFire2Down();
		bool fire1Requested = input.getFire1() || fire1Pressed;
		bool fire2Requested = input.getFire2() || fire2Pressed;
		if (rb.velocity.magnitude < maxFireSpeed)
		{
			if (fire1Requested) FireWeapon1(fire1Pressed);
			if (fire2Requested) FireOffhandWeapon1(fire2Pressed);
			if (input.getFire3()) FireWeapon3();
		}
		else if (rb.velocity.magnitude > maxSpeed * 0.6f)
		{
			// keepCameraAimWithoutArm means "camera still in aim mode while arms are lowered".
			if (isAimingRight || isAimingLeft || keepCameraAimWithoutArm)
			{
				// Keep active arm aim while moving: disable movement-triggered aim reset/toggle-off.
				// if (BeginMoveAimYaw())
				// {
				// 	pendingMoveAimToggleOff = true;
				// 	standbyDelayTimer = standbyReapplyDelay;
				// 	deferStandbyRight = moveAimYawSourceWasRight;
				// 	deferStandbyLeft = moveAimYawSourceIsLeft;
				// }
			}
			else
			{
				// Preserve reset functionality for fully non-aiming states.
				ResetWeaponAimPoint();
				startedAimingRight = false;
				startedAimingLeft = false;
				keepCameraAimWithoutArm = false;
				keepCameraAimUsesLeft = false;
				hasStoredRelativeAimRight = false;
				hasStoredRelativeAimLeft = false;
			}

			// cameraMoveScript.enabled = false;
			// aimCam.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
			//headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
		}

		bool wasAimingRight = isAimingRight;
		bool wasAimingLeft = isAimingLeft;
		bool wasKeepingCameraAimWithoutArm = keepCameraAimWithoutArm;
		bool wasKeepingCameraAimUsesLeft = keepCameraAimUsesLeft;
		bool scrollUp = input.getScrollUp();
		bool scrollDown = !scrollUp && input.getScrollDown();
		bool aimMiddle = input.getAimMiddle();
		bool consumedAimInput = UpdateOffhandMirrorAimInput(scrollUp, scrollDown, aimMiddle);
		if (!consumedAimInput)
		{
			ProcessAimScrollInput(scrollUp, scrollDown, false);
			if (aimMiddle) HandleMiddleClick();
		}
		if (AimCameraAnchorStateChanged(wasAimingRight, wasAimingLeft, wasKeepingCameraAimWithoutArm, wasKeepingCameraAimUsesLeft))
		{
			ApplyCameraImmediateForAimSwap();
		}

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

	private void SetWeaponAimPointR(Vector3 pos)
	{
		if (freezeAimPointRight)
		{
			weaponAimPoint.position = frozenAimPointRight;
			return;
		}
		weaponAimPoint.position = pos;
	}

	private bool AimCameraAnchorStateChanged(bool wasAimingRight, bool wasAimingLeft, bool wasKeepingCameraAimWithoutArm, bool wasKeepingCameraAimUsesLeft)
	{
		return wasAimingRight != isAimingRight
			|| wasAimingLeft != isAimingLeft
			|| wasKeepingCameraAimWithoutArm != keepCameraAimWithoutArm
			|| wasKeepingCameraAimUsesLeft != keepCameraAimUsesLeft;
	}

	private void ApplyCameraImmediateForAimSwap()
	{
		if (isAI)
		{
			return;
		}

		ResolveCameraMoveScript();
		if (cameraMoveScript != null)
		{
			cameraMoveScript.ApplyCameraImmediate();
		}
	}

	private void ResolveCameraMoveScript()
	{
		if (cameraMoveScript != null)
		{
			return;
		}

		if (aimCam != null)
		{
			cameraMoveScript = aimCam.GetComponentInParent<MovePlayerCamera>();
			if (cameraMoveScript == null)
			{
				cameraMoveScript = aimCam.GetComponentInChildren<MovePlayerCamera>();
			}
		}

		if (cameraMoveScript == null)
		{
			cameraMoveScript = GetComponentInChildren<MovePlayerCamera>();
		}
	}

	private void SetWeaponAimPointL(Vector3 pos)
	{
		if (freezeAimPointLeft)
		{
			weaponAimPointL.position = frozenAimPointLeft;
			return;
		}
		weaponAimPointL.position = pos;
	}

	private void ResolveAimPointIndicators()
	{
		if (rightAimPointIndicator == null && weaponAimPoint != null)
		{
			Transform indicator = weaponAimPoint.Find("target_R_point");
			if (indicator != null)
			{
				rightAimPointIndicator = indicator.gameObject;
			}
		}

		if (leftAimPointIndicator == null && weaponAimPointL != null)
		{
			Transform indicator = weaponAimPointL.Find("target_L_point");
			if (indicator != null)
			{
				leftAimPointIndicator = indicator.gameObject;
			}
		}
	}

	private void UpdateAimPointIndicatorVisibility()
	{
		ResolveAimPointIndicators();
		SetAimPointIndicatorActive(rightAimPointIndicator, ref rightAimPointIndicatorVisible, !isDead && IsRightArmAimed);
		SetAimPointIndicatorActive(leftAimPointIndicator, ref leftAimPointIndicatorVisible, !isDead && IsLeftArmAimed);
	}

	private void SetAimPointIndicatorActive(GameObject indicator, ref bool visibleCache, bool visible)
	{
		bool activeStateMatches = indicator == null || indicator.activeSelf == visible;
		if (visibleCache == visible && activeStateMatches)
		{
			return;
		}

		visibleCache = visible;

		if (indicator != null)
		{
			indicator.SetActive(visible);
		}
	}

	private void UpdateStandbyElbowTargets()
	{
		Vector3 bodyRight = Vector3.ProjectOnPlane(transform.right, Vector3.up);
		if (bodyRight.sqrMagnitude < 0.0001f)
		{
			bodyRight = Vector3.right;
		}
		else
		{
			bodyRight.Normalize();
		}

		Vector3 bodyForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
		if (bodyForward.sqrMagnitude < 0.0001f)
		{
			bodyForward = Vector3.forward;
		}
		else
		{
			bodyForward.Normalize();
		}

		UpdateStandbyElbowTarget(elbowTargetR, weaponStandbyPointR, 1f, bodyRight, bodyForward);
		UpdateStandbyElbowTarget(elbowTargetL, weaponStandbyPointL, -1f, bodyRight, bodyForward);
	}

	private void ApplyMovementStandbyVisualPose()
	{
		if (!ShouldUseMovementStandbyVisualPose())
		{
			RestoreMovementStandbyVisualAimPoints();
			return;
		}

		bool appliedAny = false;
		if (weaponAimPoint != null && weaponStandbyPointR != null)
		{
			movementStandbyStoredAimRight = weaponAimPoint.position;
			hasMovementStandbyStoredAimRight = true;
			SetWeaponAimPointR(weaponStandbyPointR.position);
			appliedAny = true;
		}

		if (weaponAimPointL != null && weaponStandbyPointL != null)
		{
			movementStandbyStoredAimLeft = weaponAimPointL.position;
			hasMovementStandbyStoredAimLeft = true;
			SetWeaponAimPointL(weaponStandbyPointL.position);
			appliedAny = true;
		}

		movementStandbyVisualActive = appliedAny;
	}

	private void RestoreMovementStandbyVisualAimPoints()
	{
		if (!movementStandbyVisualActive)
		{
			return;
		}

		if (hasMovementStandbyStoredAimRight && weaponAimPoint != null)
		{
			SetWeaponAimPointR(movementStandbyStoredAimRight);
		}
		if (hasMovementStandbyStoredAimLeft && weaponAimPointL != null)
		{
			SetWeaponAimPointL(movementStandbyStoredAimLeft);
		}

		movementStandbyVisualActive = false;
		hasMovementStandbyStoredAimRight = false;
		hasMovementStandbyStoredAimLeft = false;
	}

	private bool ShouldUseMovementStandbyVisualPose()
	{
		if (isAI || !useStandbyPoseWhileMoving || rb == null || legs == null)
		{
			return false;
		}

		float maxSpeed = legs.baseWalkSpeed * legs.getMoveSpeed();
		float maxFireSpeed = maxSpeed * 0.7f;
		return rb.velocity.magnitude >= maxFireSpeed;
	}

	private void UpdateStandbyElbowTarget(Transform elbowTarget, Transform standbyTarget, float sideSign, Vector3 bodyRight, Vector3 bodyForward)
	{
		if (elbowTarget == null || standbyTarget == null)
		{
			return;
		}

		Vector3 targetPos = standbyTarget.position;
		targetPos += Vector3.down * standbyElbowDrop;
		targetPos += bodyRight * (standbyElbowOut * sideSign);
		targetPos -= bodyForward * standbyElbowBack;
		elbowTarget.position = targetPos;
	}

	private Vector3 GetTorsoForwardWithPitch()
	{
		if (headObjectTransformCache == null)
		{
			return transform.position + transform.forward * 20f;
		}

		float pitch = 0f;
		if (aimCam != null)
		{
			pitch = aimCam.transform.localEulerAngles.x;
			if (pitch > 180f) pitch -= 360f;
		}

		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
		Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);
		return headObjectTransformCache.position + (combinedRot * Vector3.forward) * 20f;
	}

	private Vector3 GetBreakoutStartAimPoint(bool useLeft, out bool foundAimAssistTarget, out bool usedAimAssist)
	{
		float sideSign = useLeft ? -1f : 1f;
		Quaternion baseTorsoYaw = GetBreakoutAimAssistYawRotation();
		usedAimAssist = TryGetBreakoutAimAssistYaw(
			useLeft,
			baseTorsoYaw,
			out float assistedYawOffset,
			out foundAimAssistTarget,
			out int unusedVisibleTargetCount);
		float yawOffset = usedAimAssist ? assistedYawOffset : sideSign * breakoutAimYawOffset;
		return GetBreakoutStartAimPointFromYawOffset(yawOffset, usedAimAssist);
	}

	private Vector3 GetBreakoutStartAimPointFromYawOffset(float yawOffset, bool resolveWithAimRaycast = false)
	{
		float pitch = 0f;
		if (aimCam != null)
		{
			pitch = aimCam.transform.localEulerAngles.x;
			if (pitch > 180f) pitch -= 360f;
		}

		Vector3 origin = headObjectTransformCache != null ? headObjectTransformCache.position : transform.position;
		Quaternion torsoYaw = GetBreakoutAimAssistYawRotation() * Quaternion.Euler(0f, yawOffset, 0f);
		Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);
		Vector3 forward = combinedRot * Vector3.forward;
		Vector3 fallback = origin + forward * 20f;
		return resolveWithAimRaycast ? ResolveAimPoint(origin, forward, fallback) : fallback;
	}

	private bool TryGetAssistedBreakoutStartAimPoint(bool useLeft, out Vector3 aimPoint, out bool foundAimAssistTarget)
	{
		aimPoint = GetBreakoutStartAimPoint(useLeft, out foundAimAssistTarget, out bool usedAimAssist);
		return usedAimAssist;
	}

	private bool HasBreakoutAimAssistVisibleTarget(bool useLeft)
	{
		Quaternion baseTorsoYaw = GetBreakoutAimAssistYawRotation();
		TryGetBreakoutAimAssistYaw(
			useLeft,
			baseTorsoYaw,
			out float unusedYawOffset,
			out bool foundVisibleTarget,
			out int unusedVisibleTargetCount);
		return foundVisibleTarget;
	}

	private void TriggerBulletTimeForVisibleBreakoutTargets(bool useLeft)
	{
		bool usedAimAssist = TryGetSameDirectionScrollBreakoutAssist(
			useLeft,
			out Vector3 assistedAimPoint,
			out bool foundVisibleTarget,
			out int visibleTargetCount);

		if (visibleTargetCount > 1)
		{
			if (!IsFocusedArmAimedAtTarget(useLeft)
				&& TryGetClosestVisibleBreakoutTargetAimPoint(useLeft, out Vector3 closestTargetAimPoint))
			{
				ApplySameDirectionScrollAimAssist(useLeft, closestTargetAimPoint);
			}

			QueueBulletTimeTriggerForAimSwap();
			return;
		}

		if (!usedAimAssist)
		{
			if (foundVisibleTarget)
			{
				QueueBulletTimeTriggerForAimSwap();
			}
			return;
		}

		if (visibleTargetCount == 1)
		{
			ApplySameDirectionScrollAimAssist(useLeft, assistedAimPoint);
		}

		QueueBulletTimeTriggerForAimSwap();
	}

	private void ApplySameDirectionScrollAimAssist(bool useLeft, Vector3 assistedAimPoint)
	{
		if (useLeft)
		{
			aimStartHoldPointLeft = assistedAimPoint;
			BeginAimStartHold(true, aimStartHoldPointLeft);
			return;
		}

		aimStartHoldPointRight = assistedAimPoint;
		BeginAimStartHold(false, aimStartHoldPointRight);
	}

	private bool TryGetSameDirectionScrollBreakoutAssist(
		bool useLeft,
		out Vector3 aimPoint,
		out bool foundVisibleTarget,
		out int visibleTargetCount)
	{
		aimPoint = Vector3.zero;
		Quaternion baseTorsoYaw = GetBreakoutAimAssistYawRotation();
		bool usedAimAssist = TryGetBreakoutAimAssistYaw(
			useLeft,
			baseTorsoYaw,
			out float assistedYawOffset,
			out foundVisibleTarget,
			out visibleTargetCount);
		if (!usedAimAssist)
		{
			return false;
		}

		aimPoint = GetBreakoutStartAimPointFromYawOffset(assistedYawOffset, true);
		return true;
	}

	private bool IsFocusedArmAimedAtTarget(bool useLeft)
	{
		GunSelector selector = useLeft ? gunsL : guns;
		Gun primaryGun = selector != null ? selector.ActiveGun1 : null;
		Transform muzzle = primaryGun != null ? primaryGun.MuzzleTransform : null;
		if (muzzle == null)
		{
			return false;
		}

		ShootConfigScriptableObject shootConfig = primaryGun.gunData != null
			? primaryGun.gunData.shootConfig
			: null;
		LayerMask hitMask = shootConfig != null ? shootConfig.HitMask : aimMask;
		float maxRange = shootConfig != null ? Mathf.Max(0f, shootConfig.maxRange) : Mathf.Infinity;
		if (!Physics.Raycast(
			muzzle.position,
			muzzle.forward,
			out RaycastHit hit,
			maxRange,
			hitMask,
			QueryTriggerInteraction.Ignore))
		{
			return false;
		}

		return GetValidBreakoutAimAssistBody(hit.collider) != null;
	}

	private bool TryGetClosestVisibleBreakoutTargetAimPoint(bool useLeft, out Vector3 aimPoint)
	{
		aimPoint = Vector3.zero;
		Quaternion torsoYaw = GetBreakoutAimAssistYawRotation();
		if (isAI || !breakoutAimAssistEnabled || breakoutAimAssistLayerMask.value == 0)
		{
			return false;
		}

		EnsureBreakoutAimAssistBuffer();
		if (!TryGetBreakoutAimAssistBox(useLeft, torsoYaw, out Vector3 boxCenter, out Vector3 halfExtents))
		{
			return false;
		}

		int count = Physics.OverlapBoxNonAlloc(
			boxCenter,
			halfExtents,
			breakoutAimAssistColliders,
			torsoYaw,
			breakoutAimAssistLayerMask,
			QueryTriggerInteraction.Collide);
		if (count <= 0)
		{
			return false;
		}

		GetFocusedArmAimRay(useLeft, out Vector3 referenceOrigin, out Vector3 referenceForward);
		Vector3 aimLockOrigin = GetAimLockOrigin();
		Vector3 bestTargetPoint = Vector3.zero;
		float bestAlignment = float.NegativeInfinity;
		bool foundTarget = false;

		breakoutAimAssistBodies.Clear();
		for (int i = 0; i < count; i++)
		{
			BodyController targetBody = GetValidBreakoutAimAssistBody(breakoutAimAssistColliders[i]);
			if (targetBody == null || breakoutAimAssistBodies.Contains(targetBody))
			{
				continue;
			}

			Vector3 targetPoint = GetBreakoutAimAssistTargetPoint(targetBody);
			if (IsBreakoutAimAssistTargetObstructed(aimLockOrigin, targetPoint))
			{
				continue;
			}

			Vector3 horizontalDirection = targetPoint - aimLockOrigin;
			horizontalDirection.y = 0f;
			if (horizontalDirection.sqrMagnitude <= 0.0001f)
			{
				continue;
			}

			Vector3 localTargetDirection = Quaternion.Inverse(torsoYaw) * horizontalDirection;
			if ((useLeft && localTargetDirection.x >= -0.01f) || (!useLeft && localTargetDirection.x <= 0.01f))
			{
				continue;
			}

			breakoutAimAssistBodies.Add(targetBody);
			Vector3 referenceDirection = targetPoint - referenceOrigin;
			if (referenceDirection.sqrMagnitude <= 0.0001f)
			{
				continue;
			}

			float alignment = Vector3.Dot(referenceForward, referenceDirection.normalized);
			if (!foundTarget || alignment > bestAlignment)
			{
				bestAlignment = alignment;
				bestTargetPoint = targetPoint;
				foundTarget = true;
			}
		}
		breakoutAimAssistBodies.Clear();

		if (!foundTarget)
		{
			return false;
		}

		Vector3 selectedDirection = bestTargetPoint - aimLockOrigin;
		selectedDirection.y = 0f;
		Vector3 localDirection = Quaternion.Inverse(torsoYaw) * selectedDirection.normalized;
		float yawOffset = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
		aimPoint = GetBreakoutStartAimPointFromYawOffset(yawOffset, true);
		return true;
	}

	private void GetFocusedArmAimRay(bool useLeft, out Vector3 origin, out Vector3 forward)
	{
		GunSelector selector = useLeft ? gunsL : guns;
		Gun primaryGun = selector != null ? selector.ActiveGun1 : null;
		Transform muzzle = primaryGun != null ? primaryGun.MuzzleTransform : null;
		if (muzzle != null)
		{
			origin = muzzle.position;
			forward = muzzle.forward.normalized;
			return;
		}

		origin = GetAimLockOrigin();
		Transform currentAimPoint = useLeft ? weaponAimPointL : weaponAimPoint;
		Vector3 fallbackDirection = currentAimPoint != null
			? currentAimPoint.position - origin
			: transform.forward;
		forward = fallbackDirection.sqrMagnitude > 0.0001f
			? fallbackDirection.normalized
			: transform.forward;
	}

	private Quaternion GetBreakoutAimAssistYawRotation()
	{
		return Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
	}

	private bool TryGetBreakoutAimAssistYaw(
		bool useLeft,
		Quaternion torsoYaw,
		out float yawOffset,
		out bool foundVisibleTarget,
		out int visibleTargetCount)
	{
		yawOffset = 0f;
		foundVisibleTarget = false;
		visibleTargetCount = 0;
		if (isAI || !breakoutAimAssistEnabled || breakoutAimAssistLayerMask.value == 0)
		{
			return false;
		}

		EnsureBreakoutAimAssistBuffer();
		if (!TryGetBreakoutAimAssistBox(useLeft, torsoYaw, out Vector3 boxCenter, out Vector3 halfExtents))
		{
			return false;
		}

		int count = Physics.OverlapBoxNonAlloc(
			boxCenter,
			halfExtents,
			breakoutAimAssistColliders,
			torsoYaw,
			breakoutAimAssistLayerMask,
			QueryTriggerInteraction.Collide);
		if (count <= 0)
		{
			return false;
		}

		breakoutAimAssistBodies.Clear();
		Vector3 origin = GetAimLockOrigin();
		Vector3 directionSum = Vector3.zero;
		for (int i = 0; i < count; i++)
		{
			BodyController targetBody = GetValidBreakoutAimAssistBody(breakoutAimAssistColliders[i]);
			if (targetBody == null || breakoutAimAssistBodies.Contains(targetBody))
			{
				continue;
			}

			Vector3 targetPoint = GetBreakoutAimAssistTargetPoint(targetBody);
			bool hasLineOfSight = !IsBreakoutAimAssistTargetObstructed(origin, targetPoint);
			if (breakoutAimAssistRequireLineOfSight && !hasLineOfSight)
			{
				continue;
			}

			Vector3 direction = targetPoint - origin;
			direction.y = 0f;
			if (direction.sqrMagnitude <= 0.0001f)
			{
				continue;
			}

			Vector3 localTargetDirection = Quaternion.Inverse(torsoYaw) * direction;
			if ((useLeft && localTargetDirection.x >= -0.01f) || (!useLeft && localTargetDirection.x <= 0.01f))
			{
				continue;
			}

			if (hasLineOfSight)
			{
				foundVisibleTarget = true;
				visibleTargetCount++;
			}

			breakoutAimAssistBodies.Add(targetBody);
			directionSum += direction.normalized;
		}

		breakoutAimAssistBodies.Clear();
		if (directionSum.sqrMagnitude <= 0.0001f)
		{
			return false;
		}

		Vector3 localDirection = Quaternion.Inverse(torsoYaw) * directionSum.normalized;
		localDirection.y = 0f;
		if (localDirection.sqrMagnitude <= 0.0001f)
		{
			return false;
		}

		yawOffset = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
		if ((useLeft && yawOffset >= -0.01f) || (!useLeft && yawOffset <= 0.01f))
		{
			return false;
		}

		return true;
	}

	private bool TryGetBreakoutAimAssistBox(
		bool useLeft,
		Quaternion yawRotation,
		out Vector3 boxCenter,
		out Vector3 halfExtents)
	{
		float sideSign = useLeft ? -1f : 1f;
		Vector3 torsoForward = yawRotation * Vector3.forward;
		Vector3 torsoRight = yawRotation * Vector3.right;
		boxCenter = transform.position
			+ Vector3.up * (Mathf.Max(0f, breakoutAimAssistBoxHeight) * 0.5f)
			+ torsoForward * breakoutAimAssistForwardOffset
			+ torsoRight * (sideSign * breakoutAimAssistSideOffset);
		halfExtents = new Vector3(
			Mathf.Max(0f, breakoutAimAssistBoxSideWidth) * 0.5f,
			Mathf.Max(0f, breakoutAimAssistBoxHeight) * 0.5f,
			Mathf.Max(0f, breakoutAimAssistBoxDepth) * 0.5f);
		return halfExtents.x > 0f && halfExtents.y > 0f && halfExtents.z > 0f;
	}

	private void UpdateBreakoutAimAssistDebugVolumes()
	{
		if (isAI || !showBreakoutAimAssistDebugVolumes || !breakoutAimAssistEnabled)
		{
			SetBreakoutAimAssistDebugVolumesActive(false);
			return;
		}

		EnsureBreakoutAimAssistDebugVolume(
			ref breakoutAimAssistDebugLeftVolume,
			ref breakoutAimAssistDebugLeftMaterial,
			"Breakout Aim Assist Left Volume");
		EnsureBreakoutAimAssistDebugVolume(
			ref breakoutAimAssistDebugRightVolume,
			ref breakoutAimAssistDebugRightMaterial,
			"Breakout Aim Assist Right Volume");

		Quaternion yawRotation = GetBreakoutAimAssistYawRotation();
		UpdateBreakoutAimAssistDebugVolume(
			breakoutAimAssistDebugLeftVolume,
			breakoutAimAssistDebugLeftMaterial,
			true,
			yawRotation,
			breakoutAimAssistDebugLeftColor);
		UpdateBreakoutAimAssistDebugVolume(
			breakoutAimAssistDebugRightVolume,
			breakoutAimAssistDebugRightMaterial,
			false,
			yawRotation,
			breakoutAimAssistDebugRightColor);
	}

	private void EnsureBreakoutAimAssistDebugVolume(
		ref GameObject volumeObject,
		ref Material volumeMaterial,
		string volumeName)
	{
		if (volumeObject != null)
		{
			return;
		}

		volumeObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		volumeObject.name = volumeName;
		volumeObject.transform.SetParent(transform, true);

		Collider volumeCollider = volumeObject.GetComponent<Collider>();
		if (volumeCollider != null)
		{
			Destroy(volumeCollider);
		}

		Shader shader = Shader.Find("Standard");
		if (shader == null)
		{
			shader = Shader.Find("Universal Render Pipeline/Lit");
		}
		if (shader == null)
		{
			shader = Shader.Find("Sprites/Default");
		}
		if (shader == null)
		{
			volumeObject.SetActive(false);
			return;
		}

		volumeMaterial = new Material(shader);
		ConfigureBreakoutAimAssistDebugMaterial(volumeMaterial, Color.white);

		MeshRenderer renderer = volumeObject.GetComponent<MeshRenderer>();
		if (renderer != null)
		{
			renderer.sharedMaterial = volumeMaterial;
			renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			renderer.receiveShadows = false;
		}
	}

	private void UpdateBreakoutAimAssistDebugVolume(
		GameObject volumeObject,
		Material volumeMaterial,
		bool useLeft,
		Quaternion yawRotation,
		Color color)
	{
		if (volumeObject == null)
		{
			return;
		}

		if (!TryGetBreakoutAimAssistBox(useLeft, yawRotation, out Vector3 boxCenter, out Vector3 halfExtents))
		{
			volumeObject.SetActive(false);
			return;
		}

		volumeObject.SetActive(true);
		volumeObject.transform.SetPositionAndRotation(boxCenter, yawRotation);
		volumeObject.transform.localScale = halfExtents * 2f;
		ConfigureBreakoutAimAssistDebugMaterial(volumeMaterial, color);
	}

	private void ConfigureBreakoutAimAssistDebugMaterial(Material material, Color color)
	{
		if (material == null)
		{
			return;
		}

		color.a = Mathf.Clamp01(breakoutAimAssistDebugAlpha);
		material.color = color;
		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", color);
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", color);
		}
		if (material.HasProperty("_Mode"))
		{
			material.SetFloat("_Mode", 3f);
		}
		if (material.HasProperty("_Surface"))
		{
			material.SetFloat("_Surface", 1f);
		}

		material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		material.SetInt("_ZWrite", 0);
		material.DisableKeyword("_ALPHATEST_ON");
		material.EnableKeyword("_ALPHABLEND_ON");
		material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
	}

	private void SetBreakoutAimAssistDebugVolumesActive(bool active)
	{
		if (breakoutAimAssistDebugLeftVolume != null)
		{
			breakoutAimAssistDebugLeftVolume.SetActive(active);
		}
		if (breakoutAimAssistDebugRightVolume != null)
		{
			breakoutAimAssistDebugRightVolume.SetActive(active);
		}
	}

	private void EnsureBreakoutAimAssistBuffer()
	{
		int targetCount = Mathf.Max(1, breakoutAimAssistMaxTargets);
		if (breakoutAimAssistColliders == null || breakoutAimAssistColliders.Length != targetCount)
		{
			breakoutAimAssistColliders = new Collider[targetCount];
		}
	}

	private BodyController GetValidBreakoutAimAssistBody(Collider candidateCollider)
	{
		if (candidateCollider == null)
		{
			return null;
		}

		BodyController targetBody = candidateCollider.GetComponentInParent<BodyController>();
		if (targetBody == null
			|| targetBody == this
			|| !targetBody.isAI
			|| targetBody.isDead
			|| (targetBody.bodyState != null && targetBody.bodyState.isDead))
		{
			return null;
		}

		return targetBody;
	}

	private Vector3 GetBreakoutAimAssistTargetPoint(BodyController targetBody)
	{
		if (targetBody != null
			&& targetBody.bodyState != null
			&& targetBody.bodyState.headCollider != null)
		{
			return targetBody.bodyState.headCollider.bounds.center;
		}

		return targetBody != null ? targetBody.transform.position : Vector3.zero;
	}

	private bool IsBreakoutAimAssistTargetObstructed(Vector3 origin, Vector3 targetPoint)
	{
		if (breakoutAimAssistObstructionMask.value == 0)
		{
			return false;
		}

		Vector3 direction = targetPoint - origin;
		float distance = direction.magnitude;
		if (distance <= 0.0001f)
		{
			return false;
		}

		return Physics.Raycast(
			origin,
			direction / distance,
			distance,
			breakoutAimAssistObstructionMask,
			QueryTriggerInteraction.Ignore);
	}

	private Quaternion GetTorsoYawPitchRotation()
	{
		float pitch = 0f;
		if (aimCam != null)
		{
			pitch = aimCam.transform.localEulerAngles.x;
			if (pitch > 180f) pitch -= 360f;
		}

		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
		return torsoYaw * Quaternion.Euler(pitch, 0f, 0f);
	}

	private void ApplyAimYawClamp(ref Vector2 headRot)
	{
		if (aimCam == null)
		{
			return;
		}

		if (hasPendingMoveAimYaw)
		{
			return;
		}

		if (aimYawLimit <= 0f)
		{
			return;
		}

		float torsoYaw = transform.eulerAngles.y;
		float aimYaw = aimCam.transform.eulerAngles.y;
		float delta = Mathf.DeltaAngle(torsoYaw, aimYaw);
		float desiredDelta = delta + headRot.y;
		if (Mathf.Abs(desiredDelta) <= aimYawLimit)
		{
			return;
		}
		bool wasAlreadyOutsideLimit = Mathf.Abs(delta) > aimYawLimit;

		float clampedDelta = Mathf.Sign(desiredDelta) * aimYawLimit;
		float overflow = desiredDelta - clampedDelta;
		float followSpeed = GetAimYawFollowSpeed();
		float maxStep = followSpeed * Time.deltaTime;
		float torsoStep = Mathf.Clamp(overflow, -maxStep, maxStep);
		transform.Rotate(0f, torsoStep, 0f, Space.World);

		if (wasAlreadyOutsideLimit)
		{
			// Aim assist can establish a valid world-space direction beyond the normal
			// manual yaw limit. Do not project that existing direction back to the limit
			// when input begins. Block only additional outward yaw, and counter-rotate
			// the head by the torso-follow step so the current world aim is preserved.
			bool inputMovesFartherOutward = Mathf.Abs(headRot.y) > 0.0001f
				&& Mathf.Sign(headRot.y) == Mathf.Sign(delta);
			if (inputMovesFartherOutward)
			{
				headRot.y = 0f;
			}

			headRot.y -= torsoStep;
			return;
		}

		float newDelta = delta - torsoStep;
		float finalDesiredDelta = newDelta + headRot.y;
		if (Mathf.Abs(finalDesiredDelta) > aimYawLimit)
		{
			float finalClamped = Mathf.Sign(finalDesiredDelta) * aimYawLimit;
			headRot.y = finalClamped - newDelta;
		}
	}

	private float GetAimYawFollowSpeed()
	{
		float followSpeed = aimYawFollowSpeedMax;
		if (aimYawInputForMaxSpeed > 0f)
		{
			float inputYaw = Mathf.Abs(lastHeadRotation.y);
			float t = Mathf.Clamp01(inputYaw / aimYawInputForMaxSpeed);
			float curvedT = aimYawFollowCurve != null ? aimYawFollowCurve.Evaluate(t) : t;
			followSpeed = Mathf.Lerp(aimYawFollowSpeedMin, aimYawFollowSpeedMax, curvedT);
		}
		return followSpeed;
	}

	private bool BeginMoveAimYaw()
	{
		if (hasPendingMoveAimYaw)
		{
			return false;
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
			return false;
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
				return false;
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
		if (isAimingRight)
		{
			freezeAimPointRight = true;
			frozenAimPointRight = weaponAimPoint.position;
		}
		if (isAimingLeft)
		{
			freezeAimPointLeft = true;
			frozenAimPointLeft = weaponAimPointL.position;
		}
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
		return true;
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
			CompleteMoveAimYaw();
			return;
		}

		pendingMoveAimYawElapsed += Time.deltaTime;
		float t = Mathf.Clamp01(pendingMoveAimYawElapsed / moveAimYawDuration);
		float curvedT = moveAimYawCurve != null ? moveAimYawCurve.Evaluate(t) : t;
		transform.rotation = Quaternion.Slerp(pendingMoveAimYawStart, pendingMoveAimYaw, curvedT);

		if (t >= 1f || Quaternion.Angle(transform.rotation, pendingMoveAimYaw) <= moveAimYawCompleteAngle)
		{
			CompleteMoveAimYaw();
		}
	}

	private void CompleteMoveAimYaw()
	{
		hasPendingMoveAimYaw = false;
		freezeHeadDuringMoveAimYaw = false;
		hasFrozenCameraRotation = false;
		standbyDelayTimer = standbyReapplyDelay;
		deferStandbyRight = false;
		deferStandbyLeft = false;
		if (pendingMoveAimToggleOff)
		{
			if (moveAimYawSourceWasRight) ToggleAimingRight();
			if (moveAimYawSourceIsLeft) ToggleAimingLeft();
			startedAimingRight = false;
			startedAimingLeft = false;
			keepCameraAimWithoutArm = false;
			keepCameraAimUsesLeft = false;
			hasStoredRelativeAimRight = false;
			hasStoredRelativeAimLeft = false;
			useStoredAimRight = false;
			useStoredAimLeft = false;
			holdAimStartRightUntilInput = false;
			holdAimStartLeftUntilInput = false;
			aimStartHoldTimerRight = 0f;
			aimStartHoldTimerLeft = 0f;
			pendingMoveAimToggleOff = false;
			if (aimSwapDuration > 0f && headAimConstraint != null)
			{
				releaseFrozenAimPointsOnSwapComplete = true;
			}
			else
			{
				ReleaseFrozenAimPoints();
			}
		}
		else
		{
			ReleaseFrozenAimPoints();
		}
		ResetWeaponAimPoint(true, true);
	}

	private void StartAimSwapBlend(float targetW0, float targetW1, float targetW2)
	{
		if (headAimConstraint == null)
		{
			return;
		}

		var a = headAimConstraint.data.sourceObjects;
		aimSwapStartWeights = new Vector3(a[0].weight, a[1].weight, a[2].weight);
		aimSwapTargetWeights = new Vector3(targetW0, targetW1, targetW2);
		aimSwapElapsed = 0f;
		isAimSwapInProgress = true;

		if (aimSwapDuration <= 0f)
		{
			ApplyAimSwapWeights(targetW0, targetW1, targetW2);
			isAimSwapInProgress = false;
			TryTriggerQueuedBulletTime(1f);
		}
	}

	private void QueueBulletTimeTriggerForAimSwap()
	{
		if (isAI)
		{
			return;
		}

		bulletTimeTriggerPending = true;
		bulletTimeTriggeredForAimSwap = false;
		if (!isAimSwapInProgress || aimSwapDuration <= 0f)
		{
			TriggerQueuedBulletTime();
		}
	}

	private void TryTriggerQueuedBulletTime(float aimSwapProgress)
	{
		if (!bulletTimeTriggerPending || bulletTimeTriggeredForAimSwap)
		{
			return;
		}

		if (aimSwapProgress < BulletTimeManager.TriggerBlendProgress)
		{
			return;
		}

		TriggerQueuedBulletTime();
	}

	private void TriggerQueuedBulletTime()
	{
		if (!bulletTimeTriggerPending || bulletTimeTriggeredForAimSwap)
		{
			return;
		}

		bulletTimeTriggeredForAimSwap = true;
		bulletTimeTriggerPending = false;

		if (auraManager == null)
		{
			auraManager = GetComponent<AuraManager>();
		}

		if (auraManager == null || !auraManager.TryConsumeBulletTimePulse())
		{
			return;
		}

		BulletTimeManager.Trigger();
	}

	private void SetAimStandbyImmediate()
	{
		if (headAimConstraint != null)
		{
			isAimSwapInProgress = false;
			aimSwapElapsed = 0f;
			ApplyAimSwapWeights(1f, 0f, 0f);
		}

		if (releaseFrozenAimPointsOnSwapComplete || freezeAimPointRight || freezeAimPointLeft)
		{
			releaseFrozenAimPointsOnSwapComplete = false;
			ReleaseFrozenAimPoints();
		}
	}

	private void UpdateAimSwapBlend()
	{
		if (!isAimSwapInProgress || headAimConstraint == null)
		{
			return;
		}

		if (aimSwapDuration <= 0f)
		{
			isAimSwapInProgress = false;
			return;
		}

		aimSwapElapsed += Time.deltaTime;
		float t = Mathf.Clamp01(aimSwapElapsed / aimSwapDuration);
		TryTriggerQueuedBulletTime(t);
		float curvedT = aimSwapCurve != null ? aimSwapCurve.Evaluate(t) : t;
		Vector3 w = Vector3.Lerp(aimSwapStartWeights, aimSwapTargetWeights, curvedT);
		ApplyAimSwapWeights(w.x, w.y, w.z);

		if (t >= 1f)
		{
			isAimSwapInProgress = false;
			if (releaseFrozenAimPointsOnSwapComplete)
			{
				releaseFrozenAimPointsOnSwapComplete = false;
				ReleaseFrozenAimPoints();
			}
		}
	}

	private void ApplyAimSwapWeights(float w0, float w1, float w2)
	{
		var a = headAimConstraint.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		var a2 = a[2];
		a0.weight = w0;
		a1.weight = w1;
		a2.weight = w2;
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		headAimConstraint.data.sourceObjects = a;
	}

	private void ReleaseFrozenAimPoints()
	{
		freezeAimPointRight = false;
		freezeAimPointLeft = false;
	}

	// AI duplicated aim path methods (intentionally identical for now)
	private void ExecutePhysicsBasedInputsAI()
	{
		bool isSlowWalking = !isAI && input != null && input.getShift();
		legs.speedMultiplier = isAI ? 1f : (isSlowWalking ? 0.3f : 1f);

		if (legs.isCurrentVelocityLessThanMax())
		{
			if (input.getForward()) MoveForward();
			if (input.getBackward()) MoveBackward();
			if (input.getLeft()) MoveLeft();
			if (input.getRight()) MoveRight();
		}

		float maxSpeed = legs.baseWalkSpeed * legs.getMoveSpeed();
		float maxFireSpeed = maxSpeed * 0.5f;
		if (rb.velocity.magnitude < maxFireSpeed)
		{
			if (input.getFire1()) FireWeapon1();
			if (input.getFire2()) FireWeapon2();
			if (input.getFire3()) FireWeapon3();
		}
		else if (rb.velocity.magnitude > maxSpeed * 0.6f)
		{
			if (isAimingRight || isAimingLeft)
			{
				if (BeginMoveAimYawAI())
				{
					pendingMoveAimToggleOff = true;
				}
			}
			else
			{
				ResetWeaponAimPointAI();
				startedAimingRight = false;
				startedAimingLeft = false;
				keepCameraAimWithoutArm = false;
				keepCameraAimUsesLeft = false;
				hasStoredRelativeAimRight = false;
				hasStoredRelativeAimLeft = false;
			}
		}

		bool scrollUp = input.getScrollUp();
		bool scrollDown = !scrollUp && input.getScrollDown();
		ProcessAimScrollInput(scrollUp, scrollDown, true);
		if (input.getAimMiddle()) HandleMiddleClickAI();

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
	}

	private void DoRotationAI()
	{
		Vector2 headRot = input.getHeadRotation();
		lastHeadRotation = headRot;
		if (isAimingRight || isAimingLeft)
		{
			ApplyAimYawClampAI(ref headRot);
			sensors.setHeadRotation(headRot);
		}
		else
		{
			sensors.setHeadRotation(new Vector2(headRot.x, 0));
			transform.Rotate(0, headRot.y, 0);
		}
	}

	private void HandleScrollUpAI()
	{
		if (isAimingLeft)
		{
			ToggleAimingLeftAI();
			return;
		}

		if (!startedAimingLeft && hasStoredRelativeAimLeft)
		{
			if (IsLoweredSideSelected(true))
			{
				ToggleAimingLeftAI();
				return;
			}

			SwitchToLoweredSideAI(true);
			return;
		}

		ToggleAimingLeftAI();
	}

	private void HandleScrollDownAI()
	{
		if (isAimingRight)
		{
			ToggleAimingRightAI();
			return;
		}

		if (!startedAimingRight && hasStoredRelativeAimRight)
		{
			if (IsLoweredSideSelected(false))
			{
				ToggleAimingRightAI();
				return;
			}

			SwitchToLoweredSideAI(false);
			return;
		}

		ToggleAimingRightAI();
	}

	private void HandleMiddleClickAI()
	{
		if (isAimingLeft)
		{
			ToggleAimingLeftAI();
			return;
		}

		if (isAimingRight)
		{
			ToggleAimingRightAI();
			return;
		}
	}

	private void ToggleAimingRightAI()
	{
		isAimingLeft = false;

		isAimingRight = !isAimingRight;
		if (isAimingRight)
		{
			if (!startedAimingRight)
			{
				startedAimingRight = true;
			}

			StartAimSwapBlendAI(0f, 1f, 0f);
		}
		else
		{
			SetAimStandbyImmediate();
		}
	}

	private void ToggleAimingLeftAI()
	{
		isAimingRight = false;

		isAimingLeft = !isAimingLeft;
		if (isAimingLeft)
		{
			if (!startedAimingLeft)
			{
				startedAimingLeft = true;
			}

			StartAimSwapBlendAI(0f, 0f, 1f);
		}
		else
		{
			SetAimStandbyImmediate();
		}
	}

	private void GetAimPointAI()
	{
		if (Time.time - lastRaycastTime >= raycastInterval)
		{
			Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
			float pitch = aimCam.transform.localEulerAngles.x;
			if (pitch > 180f) pitch -= 360f;

			Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);
			Vector3 pitchedForward = combinedRot * Vector3.forward;
			Vector3 torso = headObjectTransformCache.position + pitchedForward * 20f;

			if (freezeHeadDuringMoveAimYaw)
			{
				torsoAimPoint.position = torso;
				return;
			}

			if (rb.velocity.magnitude > 2.5f)
			{
				torsoAimPoint.position = torso;
				weaponAimPoint.position = torso;
				weaponAimPointL.position = torso;
				return;
			}

			if (isAimingRight || isAimingLeft)
			{
				combinedRot = Quaternion.Euler(aimCam.transform.eulerAngles.x,
																			 aimCam.transform.eulerAngles.y,
																			 0f);
			}
			else
			{
				torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

				pitch = aimCam.transform.localEulerAngles.x;
				if (pitch > 180f) pitch -= 360f;

				combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);

				if (!startedAimingRight && !startedAimingLeft && !freezeHeadDuringMoveAimYaw)
				{
					headObject.transform.SetPositionAndRotation(headObjectTransformCache.transform.position, headObjectTransformCache.transform.rotation);
				}
			}

			Vector3 forward = combinedRot * Vector3.forward;
			torso = headObjectTransformCache.position + forward * 20f;

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

					foreach (var collider in bodyColliders)
					{
						if (hit.collider == collider)
						{
							isOwnCollider = true;
							break;
						}
					}

					if (!isOwnCollider && hit.collider.gameObject.layer == 6)
					{
						bodyHit = hit;
						break;
					}
				}

				if (bodyHit.HasValue)
				{
					if (enviroHits.Count > 0)
					{
						enviroHits.Sort((hit1, hit2) => hit1.distance.CompareTo(hit2.distance));

						if (Vector3.Distance(rb.transform.position, bodyHit.Value.point) < Vector3.Distance(rb.transform.position, enviroHits[0].point))
						{
							weaponAimPoint.position = bodyHit.Value.point;
						}
						else
						{
							weaponAimPoint.position = enviroHits[0].point;
						}
					}
					else
					{
						weaponAimPoint.position = bodyHit.Value.point;
					}
				}
				else if (enviroHits.Count > 0)
				{
					enviroHits.Sort((hit1, hit2) => hit1.distance.CompareTo(hit2.distance));
					weaponAimPoint.position = enviroHits[0].point;
				}
				else
				{
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
				ResetWeaponAimPointAI();
			}
		}
	}

	private void ResetWeaponAimPointAI(bool resetPitch = false, bool resetHead = true)
	{
		Quaternion torsoYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
		float pitch = aimCam.transform.localEulerAngles.x;
		if (pitch > 180f) pitch -= 360f;

		Quaternion combinedRot = torsoYaw * Quaternion.Euler(pitch, 0f, 0f);
		Vector3 pitchedForward = combinedRot * Vector3.forward;
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
	}

	private void ApplyAimYawClampAI(ref Vector2 headRot)
	{
		if (aimCam == null)
		{
			return;
		}

		if (hasPendingMoveAimYaw)
		{
			return;
		}

		if (aimYawLimit <= 0f)
		{
			return;
		}

		float torsoYaw = transform.eulerAngles.y;
		float aimYaw = aimCam.transform.eulerAngles.y;
		float delta = Mathf.DeltaAngle(torsoYaw, aimYaw);
		float desiredDelta = delta + headRot.y;
		if (Mathf.Abs(desiredDelta) <= aimYawLimit)
		{
			return;
		}

		float clampedDelta = Mathf.Sign(desiredDelta) * aimYawLimit;
		float overflow = desiredDelta - clampedDelta;
		float followSpeed = GetAimYawFollowSpeedAI();
		float maxStep = followSpeed * Time.deltaTime;
		float torsoStep = Mathf.Clamp(overflow, -maxStep, maxStep);
		transform.Rotate(0f, torsoStep, 0f, Space.World);

		float newDelta = delta - torsoStep;
		float finalDesiredDelta = newDelta + headRot.y;
		if (Mathf.Abs(finalDesiredDelta) > aimYawLimit)
		{
			float finalClamped = Mathf.Sign(finalDesiredDelta) * aimYawLimit;
			headRot.y = finalClamped - newDelta;
		}
	}

	private float GetAimYawFollowSpeedAI()
	{
		float followSpeed = aimYawFollowSpeedMax;
		if (aimYawInputForMaxSpeed > 0f)
		{
			float inputYaw = Mathf.Abs(lastHeadRotation.y);
			float t = Mathf.Clamp01(inputYaw / aimYawInputForMaxSpeed);
			float curvedT = aimYawFollowCurve != null ? aimYawFollowCurve.Evaluate(t) : t;
			followSpeed = Mathf.Lerp(aimYawFollowSpeedMin, aimYawFollowSpeedMax, curvedT);
		}
		return followSpeed;
	}

	private bool BeginMoveAimYawAI()
	{
		if (hasPendingMoveAimYaw)
		{
			return false;
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
			return false;
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
				return false;
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
		return true;
	}

	private void UpdatePendingMoveAimYawAI()
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
			CompleteMoveAimYawAI();
			return;
		}

		pendingMoveAimYawElapsed += Time.deltaTime;
		float t = Mathf.Clamp01(pendingMoveAimYawElapsed / moveAimYawDuration);
		float curvedT = moveAimYawCurve != null ? moveAimYawCurve.Evaluate(t) : t;
		transform.rotation = Quaternion.Slerp(pendingMoveAimYawStart, pendingMoveAimYaw, curvedT);

		if (t >= 1f || Quaternion.Angle(transform.rotation, pendingMoveAimYaw) <= moveAimYawCompleteAngle)
		{
			CompleteMoveAimYawAI();
		}
	}

	private void CompleteMoveAimYawAI()
	{
		hasPendingMoveAimYaw = false;
		freezeHeadDuringMoveAimYaw = false;
		hasFrozenCameraRotation = false;
		if (pendingMoveAimToggleOff)
		{
			if (moveAimYawSourceWasRight) ToggleAimingRightAI();
			if (moveAimYawSourceIsLeft) ToggleAimingLeftAI();
			startedAimingRight = false;
			startedAimingLeft = false;
			keepCameraAimWithoutArm = false;
			keepCameraAimUsesLeft = false;
			hasStoredRelativeAimRight = false;
			hasStoredRelativeAimLeft = false;
			pendingMoveAimToggleOff = false;
		}
		ResetWeaponAimPointAI(true, true);
	}

	private void StartAimSwapBlendAI(float targetW0, float targetW1, float targetW2)
	{
		if (headAimConstraint == null)
		{
			return;
		}

		var a = headAimConstraint.data.sourceObjects;
		aimSwapStartWeights = new Vector3(a[0].weight, a[1].weight, a[2].weight);
		aimSwapTargetWeights = new Vector3(targetW0, targetW1, targetW2);
		aimSwapElapsed = 0f;
		isAimSwapInProgress = true;

		if (aimSwapDuration <= 0f)
		{
			ApplyAimSwapWeightsAI(targetW0, targetW1, targetW2);
			isAimSwapInProgress = false;
		}
	}

	private void UpdateAimSwapBlendAI()
	{
		if (!isAimSwapInProgress || headAimConstraint == null)
		{
			return;
		}

		if (aimSwapDuration <= 0f)
		{
			isAimSwapInProgress = false;
			return;
		}

		aimSwapElapsed += Time.deltaTime;
		float t = Mathf.Clamp01(aimSwapElapsed / aimSwapDuration);
		float curvedT = aimSwapCurve != null ? aimSwapCurve.Evaluate(t) : t;
		Vector3 w = Vector3.Lerp(aimSwapStartWeights, aimSwapTargetWeights, curvedT);
		ApplyAimSwapWeightsAI(w.x, w.y, w.z);

		if (t >= 1f)
		{
			isAimSwapInProgress = false;
		}
	}

	private void ApplyAimSwapWeightsAI(float w0, float w1, float w2)
	{
		var a = headAimConstraint.data.sourceObjects;
		var a0 = a[0];
		var a1 = a[1];
		var a2 = a[2];
		a0.weight = w0;
		a1.weight = w1;
		a2.weight = w2;
		a[0] = a0;
		a[1] = a1;
		a[2] = a2;
		headAimConstraint.data.sourceObjects = a;
	}
}
