using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable]
public struct BoneJointPair
{
	public Transform bone;
	public ConfigurableJoint joint;
}

public class ActiveRagdollController : MonoBehaviour, IEnemyPoolResettable
{
	public BoneJointPair[] bonesAndJoints;
	private Quaternion[] _initialJointsRotation;
	private Rigidbody[] Rigidbodies;
	public Transform AnimatedRightFoot;
	public Transform AnimatedLeftFoot;
	public Transform RagdollRightFoot;
	public Transform RagdollLeftFoot;
	public Transform RagdollRightArm;
	public Transform RagdollRightWeapon;
	public Transform RagdollLeftArm;

	public Transform RagdollLeftWeapon;

	public Transform AnimatedHead;
	public Rigidbody RagdollHead;
	public Rigidbody RagdollLeftFootRb;
	public Rigidbody RagdollRightFootRb;
	public Rigidbody RagdollRightArmRb;
	public Rigidbody RagdollLeftArmRb;
	public Rigidbody RagdollSpineLowerRb;
	public AnimationCurve uprightTorqueFunction;
	public float uprightTorque = 10000;
	public float rotationTorque = 500;

	public float upwardStabilizerForce = 15f;

	public float downwardStabilizerForce = 10f;

	public BodyState bodyState;

	public Vector3 TargetDirection { get; set; }
	private Quaternion _targetRotation;
	public Transform AnimatedRightArm;
	public Transform AnimatedRightWeapon;
	public Transform target;
	public Transform targetL;


	public ProceduralAnimation proceduralAnimation;
	private float previousRotation;
	private float angularSpeed;

	public float stepDuration = 0.3f;
	public float stepHeight = 1.5f;


	public Transform leftFoot;

	public Transform rightFoot;

	public Transform leftTarget;

	public Transform rightTarget;

	public BodyController bodyController;

	public Rigidbody rb;

	public float headStabilizerForce;
	private Vector3 lastVelocity;


	public Transform realHead; // Reference to the realHead GameObject
	public float positionSmoothTime = 0.01f; // Adjust to control position smoothing
	public float rotationSmoothTime = 0.01f; // Adjust to control rotation smoothing

	private Vector3 positionVelocity = Vector3.zero;
	private Vector3 rotationVelocity = Vector3.zero;

	[Header("Arm Aim Follow")]
	[SerializeField] private float armAimFollowSpeed = 12f;
	// [SerializeField] private float armAimSwapCatchUpSpeed = 40f;
	// [SerializeField] private float armAimSwapCatchUpDuration = 0.08f;
	[SerializeField] private float minAimDirectionSqrMagnitude = 0.0001f;
	private bool wasAimingRight;
	private bool wasAimingLeft;
	// private float rightAimSwapCatchUpTimer;
	// private float leftAimSwapCatchUpTimer;

	[Header("Assisted Aim Travel")]
	[SerializeField, Min(0f)] private float assistedAimMinTravelTime = 0.06f;
	[SerializeField, Min(0f)] private float assistedAimMaxTravelTime = 0.25f;
	[SerializeField, Min(0.0001f)] private float assistedAimAngleForMaxTravelTime = 100f;

	private sealed class AssistedAimTravelState
	{
		public bool active;
		public float elapsed;
		public float duration;
		public Quaternion startArmRotation;
		public Quaternion startWeaponRotation;
	}

	private readonly AssistedAimTravelState rightAssistedAimTravel = new AssistedAimTravelState();
	private readonly AssistedAimTravelState leftAssistedAimTravel = new AssistedAimTravelState();

	[Header("Movement Aim Error")]
	[SerializeField] private bool enableMovementAimError = true;
	[SerializeField] private float movementAimErrorMaxDegrees = 35f;
	[SerializeField] private float movementAimErrorMinSpeed = 0.15f;
	[SerializeField] private float movementAimErrorSpeedForMax = 2.5f;
	[SerializeField] private float movementAimErrorRetargetInterval = 0.12f;
	[SerializeField] private float movementAimErrorFollowSpeed = 18f;
	[SerializeField] private float movementAimErrorSettleSpeed = 14f;
	private Vector2 currentMovementAimError;
	private Vector2 targetMovementAimError;
	private float movementAimErrorRetargetTimer;
	private Quaternion movementAimErrorRotation = Quaternion.identity;

	[Header("Movement Aim Shot Error")]
	[SerializeField] private float movementShotErrorMaxDegrees = 45f;
	[SerializeField] private float movementShotErrorMinSpeed = 0.15f;
	[SerializeField] private float movementShotErrorSpeedForMax = 2.5f;
	[SerializeField] private float movementShotErrorDecaySpeed = 8f;
	private Vector2 currentMovementShotError;
	private Quaternion movementShotErrorRotation = Quaternion.identity;
	private bool ownerIsPlayer;

	// Start is called before the first frame update
	void Start()
	{
		_initialJointsRotation = new Quaternion[bonesAndJoints.Length];
		for (int i = 0; i < bonesAndJoints.Length; i++)
		{
			_initialJointsRotation[i] = bonesAndJoints[i].bone.localRotation;
		}
		//for (int i = 0; i < bonesAndJoints.Length; i++)
		//{
		//    ConfigurableJointExtensions.SetupAsCharacterJoint(bonesAndJoints[i].joint);
		//}
		Rigidbodies = this.GetComponentsInChildren<Rigidbody>();

		foreach (Rigidbody rb in Rigidbodies)
		{
			rb.solverIterations = 70;
			//rb.solverVelocityIterations = 20;
			//rb.maxAngularVelocity = 20;
		}
		lastVelocity = rb.velocity;
		//previousRotation = proceduralAnimation.pivot.transform.rotation.x;

		//setUp();
		bodyController = GetComponentInParent<BodyController>();
		bodyState = GetComponentInParent<BodyState>();
		ownerIsPlayer = bodyController != null && bodyController.GetComponentInParent<PlayerController>() != null;

	}

	public void ResetForPoolReuse()
	{
		rightAssistedAimTravel.active = false;
		rightAssistedAimTravel.elapsed = 0f;
		leftAssistedAimTravel.active = false;
		leftAssistedAimTravel.elapsed = 0f;
		wasAimingRight = false;
		wasAimingLeft = false;
		currentMovementAimError = Vector2.zero;
		targetMovementAimError = Vector2.zero;
		movementAimErrorRetargetTimer = 0f;
		movementAimErrorRotation = Quaternion.identity;
		currentMovementShotError = Vector2.zero;
		movementShotErrorRotation = Quaternion.identity;
		positionVelocity = Vector3.zero;
		rotationVelocity = Vector3.zero;
		TargetDirection = transform.forward;
		_targetRotation = transform.rotation;
		lastVelocity = rb != null ? rb.velocity : Vector3.zero;
	}

	// Update is called once per frame
	void FixedUpdate()
	{
		UpdateJointTargets();
		//RagdollLeftFoot = AnimatedLeftFoot;
		//RagdollRightFoot = AnimatedRightFoot;
		UpdateTargetRotation();
		//ApplyUprightTorque();
		// if (proceduralAnimation.leftFootTargetRig.localPosition.y > -0.45f)
		// {
		// 	RagdollLeftFootRb.isKinematic = false;
		// }
		// else
		// {
		// 	RagdollLeftFootRb.isKinematic = true;
		// }

		// if (proceduralAnimation.rightFootTargetRig.localPosition.y > -0.45f)
		// {
		// 	RagdollLeftFootRb.isKinematic = false;
		// }
		// else
		// {
		// 	RagdollRightFootRb.isKinematic = true;
		// }
		//StartCoroutine(step(rightFoot, rightTarget));
		//StartCoroutine(step(leftFoot, leftTarget));
		// rightFoot.position = rightTarget.position;
		// leftFoot.position = leftTarget.position;


		//RagdollHead.transform.position = Vector3.SmoothDamp(RagdollHead.transform.position, realHead.position, ref positionVelocity, positionSmoothTime);

		//RagdollHead.transform.position = realHead.position;

		// Smoothly interpolate the rotation
		//Quaternion targetRotation = Quaternion.Euler(Vector3.SmoothDamp(RagdollHead.transform.rotation.eulerAngles, realHead.rotation.eulerAngles, ref rotationVelocity, rotationSmoothTime));
		//RagdollHead.transform.rotation = targetRotation;
		//RagdollHead.transform.rotation = realHead.rotation;

		ApplyHeadForce();
	}

	private void ApplyHeadForce()
	{
		Vector3 currentVelocity = rb.velocity;
		if (Mathf.Abs(currentVelocity.x - lastVelocity.x) > Mathf.Epsilon)
		{
			// Apply a force to the head in the direction of the player's movement
			RagdollHead.AddForce(currentVelocity * headStabilizerForce * GetActiveRagdollScale(), ForceMode.Acceleration);
		}

		lastVelocity = currentVelocity;
	}

	public void setUp()
	{
		Transform transform = new GameObject("Left IK Target").transform;
		Transform transform2 = new GameObject("Right IK Target").transform;
		transform.parent = leftFoot;
		transform.localPosition = Vector3.zero;
		transform2.parent = rightFoot;
		transform2.localPosition = Vector3.zero;
		transform.parent = base.transform;
		transform2.parent = base.transform;
		Transform transform3 = new GameObject("Left IK Pole").transform;
		Transform transform4 = new GameObject("Right IK Pole").transform;
		transform3.position = new Vector3(transform.position.x, RagdollSpineLowerRb.position.y, transform.position.z + 1f);
		transform4.position = new Vector3(transform2.position.x, RagdollSpineLowerRb.position.y, transform2.position.z + 1f);
		transform3.parent = RagdollSpineLowerRb.transform;
		transform4.parent = RagdollSpineLowerRb.transform;
		InverseKinematics inverseKinematics = leftFoot.gameObject.AddComponent<InverseKinematics>();
		InverseKinematics inverseKinematics2 = rightFoot.gameObject.AddComponent<InverseKinematics>();
		inverseKinematics.Target = transform;
		inverseKinematics2.Target = transform2;
		inverseKinematics.Pole = transform3;
		inverseKinematics2.Pole = transform4;
		leftFoot = transform;
		rightFoot = transform2;
	}

	private IEnumerator step(Transform foot, Transform target)
	{
		Vector3 startPoint = foot.position;
		Vector3 centerPoint = (foot.position + target.position) / 2f;
		centerPoint.y = target.position.y + stepHeight;
		float timeElapsed = 0f;
		do
		{
			timeElapsed += Time.deltaTime;
			float t = timeElapsed / stepDuration;
			foot.position = Vector3.Lerp(Vector3.Lerp(startPoint, centerPoint, t), Vector3.Lerp(centerPoint, target.position, t), t);
			yield return null;
		}
		while (timeElapsed < stepDuration);
	}

	private float CalculateAngularSpeed(Transform t)
	{
		angularSpeed = Math.Abs(t.rotation.x - previousRotation);

		// Update the previous rotation
		previousRotation = t.rotation.x;

		return angularSpeed;
	}

	private void ApplyUprightTorque()
	{
		var balancePercent = Vector3.Angle(RagdollSpineLowerRb.transform.up,
														 Vector3.up) / 180;
		balancePercent = uprightTorqueFunction.Evaluate(balancePercent);
		var rot = Quaternion.FromToRotation(RagdollSpineLowerRb.transform.up,
											 Vector3.up).normalized;

		float ragdollScale = GetActiveRagdollScale();
		RagdollSpineLowerRb.AddTorque(new Vector3(0, rot.y, 0)
															* uprightTorque * balancePercent * ragdollScale);
		// RagdollSpineLowerRb.AddTorque(new Vector3(rot.x, rot.y, rot.z)
		// 											* uprightTorque * balancePercent);

		var directionAnglePercent = Vector3.SignedAngle(RagdollSpineLowerRb.transform.forward,
							TargetDirection, Vector3.up) / 180;
		RagdollSpineLowerRb.AddRelativeTorque(0, directionAnglePercent * rotationTorque * ragdollScale, 0);

		if (RagdollSpineLowerRb.position.y < 1.31f)
		{
			RagdollSpineLowerRb.AddForce(new Vector3(0, upwardStabilizerForce * ragdollScale, 0), ForceMode.Acceleration);

			// RagdollSpineLowerRb.AddForce(new Vector3(0, 7000, 0), ForceMode.Force);
		}
		else if (RagdollSpineLowerRb.position.y < 1.32f)
		{
			RagdollSpineLowerRb.AddForce(new Vector3(0, -downwardStabilizerForce * ragdollScale, 0), ForceMode.Acceleration);
		}
	}

	private void UpdateTargetRotation()
	{
		if (TargetDirection != Vector3.zero)
			_targetRotation = Quaternion.LookRotation(TargetDirection, Vector3.up);
		else
			_targetRotation = Quaternion.identity;
	}

	private void LateUpdate()
	{
		// if (RagdollRightArmRb.velocity.magnitude < 0.5f && rb.velocity.magnitude < 0.1f)
		// {

		if (bodyState.hitStunAmount <= 0 && !bodyController.isDead)
		{
			bool hasAimedArm = bodyController.IsRightArmAimed || bodyController.IsLeftArmAimed;
			UpdateMovementAimError(hasAimedArm);
			UpdateMovementShotError(hasAimedArm);
			UpdateAimSwapCatchUpTimers();

			// float rightSpeed = rightAimSwapCatchUpTimer > 0f ? armAimSwapCatchUpSpeed : armAimFollowSpeed;
			// float leftSpeed = leftAimSwapCatchUpTimer > 0f ? armAimSwapCatchUpSpeed : armAimFollowSpeed;
			float rightSpeed = armAimFollowSpeed;
			float leftSpeed = armAimFollowSpeed;

			if (rightAssistedAimTravel.active)
			{
				UpdateBreakoutAimAssistTravel(
					rightAssistedAimTravel,
					RagdollRightArm,
					RagdollRightWeapon,
					target,
					bodyController.guns,
					bodyController.IsRightArmAimed);
			}
			else
			{
				UpdateArmAim(RagdollRightArm, RagdollRightWeapon, target, bodyController.guns, rightSpeed, false, bodyController.IsRightArmAimed);
			}

			if (leftAssistedAimTravel.active)
			{
				UpdateBreakoutAimAssistTravel(
					leftAssistedAimTravel,
					RagdollLeftArm,
					RagdollLeftWeapon,
					targetL,
					bodyController.gunsL,
					bodyController.IsLeftArmAimed);
			}
			else
			{
				UpdateArmAim(RagdollLeftArm, RagdollLeftWeapon, targetL, bodyController.gunsL, leftSpeed, false, bodyController.IsLeftArmAimed);
			}

			// rightAimSwapCatchUpTimer = Mathf.Max(0f, rightAimSwapCatchUpTimer - Time.deltaTime);
			// leftAimSwapCatchUpTimer = Mathf.Max(0f, leftAimSwapCatchUpTimer - Time.deltaTime);

		}
		else
		{
			CancelAllBreakoutAimAssistTravel();
			UpdateMovementAimError(false);
			UpdateMovementShotError(false);
		}

		//AnimatedRightWeapon.transform.rotation = t;
		//RagdollRightArm.LookAt(target.transform.position, Vector3.up);
		//AnimatedRightArm.LookAt(target.transform.position, Vector3.up);
		//RagdollRightWeapon.transform.rotation = t;

		// Debug.Log(CalculateAngularSpeed(proceduralAnimation.pivot));
		// if (CalculateAngularSpeed(proceduralAnimation.pivot) < Mathf.Epsilon)
		// {
		// 	RagdollLeftFootRb.isKinematic = true;
		// 	RagdollRightFootRb.isKinematic = true;
		// }
		// else
		// {
		// 	RagdollLeftFootRb.isKinematic = false;
		// 	RagdollRightFootRb.isKinematic = false;
		// }
		//}
			//RagdollHead.transform.rotation = AnimatedHead.rotation;
		}

	private void UpdateAimSwapCatchUpTimers()
	{
		bool isAimingRight = bodyController != null && bodyController.IsRightArmAimed;
		bool isAimingLeft = bodyController != null && bodyController.IsLeftArmAimed;

		if (isAimingRight && !wasAimingRight && !rightAssistedAimTravel.active)
		{
			// rightAimSwapCatchUpTimer = armAimSwapCatchUpDuration;
			UpdateArmAim(RagdollRightArm, RagdollRightWeapon, target, bodyController.guns, armAimFollowSpeed, true, true);
		}

		if (isAimingLeft && !wasAimingLeft && !leftAssistedAimTravel.active)
		{
			// leftAimSwapCatchUpTimer = armAimSwapCatchUpDuration;
			UpdateArmAim(RagdollLeftArm, RagdollLeftWeapon, targetL, bodyController.gunsL, armAimFollowSpeed, true, true);
		}

		wasAimingRight = isAimingRight;
		wasAimingLeft = isAimingLeft;
	}

	private void UpdateArmAim(Transform arm, Transform weapon, Transform aimTarget, GunSelector gunSelector, float followSpeed, bool instant, bool applyMovementAimError)
	{
		if (!TryGetArmAimRotations(
			arm,
			weapon,
			aimTarget,
			gunSelector,
			applyMovementAimError,
			out Quaternion armRotation,
			out Quaternion weaponRotation))
		{
			return;
		}

		float t = instant ? 1f : GetAimFollowT(followSpeed);
		arm.rotation = Quaternion.Slerp(arm.rotation, armRotation, t);
		weapon.rotation = Quaternion.Slerp(weapon.rotation, weaponRotation, t);
	}

	public void BeginBreakoutAimAssistTravel(bool useLeft)
	{
		AssistedAimTravelState state = useLeft ? leftAssistedAimTravel : rightAssistedAimTravel;
		Transform arm = useLeft ? RagdollLeftArm : RagdollRightArm;
		Transform weapon = useLeft ? RagdollLeftWeapon : RagdollRightWeapon;
		Transform aimTarget = useLeft ? targetL : target;
		GunSelector gunSelector = bodyController != null
			? (useLeft ? bodyController.gunsL : bodyController.guns)
			: null;

		if (!TryGetArmAimRotations(
			arm,
			weapon,
			aimTarget,
			gunSelector,
			true,
			out Quaternion targetArmRotation,
			out _))
		{
			state.active = false;
			return;
		}

		float minTravelTime = Mathf.Max(0f, assistedAimMinTravelTime);
		float maxTravelTime = Mathf.Max(minTravelTime, assistedAimMaxTravelTime);
		float angleForMaxTravelTime = Mathf.Max(0.0001f, assistedAimAngleForMaxTravelTime);
		float angularDistance = Quaternion.Angle(arm.rotation, targetArmRotation);
		float angleT = Mathf.Clamp01(angularDistance / angleForMaxTravelTime);

		state.elapsed = 0f;
		state.duration = Mathf.Lerp(minTravelTime, maxTravelTime, angleT);
		state.startArmRotation = arm.rotation;
		state.startWeaponRotation = weapon.rotation;
		state.active = true;
	}

	public void CancelBreakoutAimAssistTravel(bool useLeft)
	{
		(useLeft ? leftAssistedAimTravel : rightAssistedAimTravel).active = false;
	}

	public void CancelAllBreakoutAimAssistTravel()
	{
		rightAssistedAimTravel.active = false;
		leftAssistedAimTravel.active = false;
	}

	private void UpdateBreakoutAimAssistTravel(
		AssistedAimTravelState state,
		Transform arm,
		Transform weapon,
		Transform aimTarget,
		GunSelector gunSelector,
		bool applyMovementAimError)
	{
		if (!TryGetArmAimRotations(
			arm,
			weapon,
			aimTarget,
			gunSelector,
			applyMovementAimError,
			out Quaternion targetArmRotation,
			out Quaternion targetWeaponRotation))
		{
			state.active = false;
			return;
		}

		state.elapsed += Time.unscaledDeltaTime;
		float progress = state.duration <= 0f
			? 1f
			: Mathf.Clamp01(state.elapsed / state.duration);
		float easedProgress = SmootherStep(progress);

		arm.rotation = Quaternion.Slerp(state.startArmRotation, targetArmRotation, easedProgress);
		weapon.rotation = Quaternion.Slerp(state.startWeaponRotation, targetWeaponRotation, easedProgress);

		if (progress >= 1f)
		{
			state.active = false;
		}
	}

	private bool TryGetArmAimRotations(
		Transform arm,
		Transform weapon,
		Transform aimTarget,
		GunSelector gunSelector,
		bool applyMovementAimError,
		out Quaternion armRotation,
		out Quaternion weaponRotation)
	{
		armRotation = arm != null ? arm.rotation : Quaternion.identity;
		weaponRotation = weapon != null ? weapon.rotation : Quaternion.identity;
		if (arm == null || weapon == null || aimTarget == null)
		{
			return false;
		}

		Vector3 direction = aimTarget.position - arm.position;
		if (!TryGetAimRotation(direction, transform.up, arm.rotation, out armRotation))
		{
			return false;
		}
		armRotation = ApplyMovementAimError(armRotation, applyMovementAimError);

		if (gunSelector != null)
		{
			weaponRotation = gunSelector.GetPrimaryHandAimRotation(aimTarget.position, transform.right);
		}
		else
		{
			Vector3 weaponDirection = aimTarget.position - weapon.position;
			if (!TryGetAimRotation(weaponDirection, transform.right, weapon.rotation, out weaponRotation))
			{
				return false;
			}
		}

		weaponRotation = ApplyMovementAimError(weaponRotation, applyMovementAimError);
		return true;
	}

	private static float SmootherStep(float t)
	{
		t = Mathf.Clamp01(t);
		return t * t * t * (t * (t * 6f - 15f) + 10f);
	}

	public void ApplyMovementAimShotImpulse(bool isLeftArm)
	{
		if (!ShouldUseMovementAimShotError(isLeftArm))
		{
			return;
		}

		float speed = GetMovementAimErrorSpeed();
		float speedT = GetMovementAimErrorSpeedPercent(speed, movementShotErrorMinSpeed, movementShotErrorSpeedForMax);
		if (speedT <= 0f)
		{
			return;
		}

		float coneDegrees = Mathf.Max(0f, movementShotErrorMaxDegrees) * speedT;
		currentMovementShotError = UnityEngine.Random.insideUnitCircle * coneDegrees;
		UpdateMovementShotErrorRotation();
		UpdateArmAimForShotImpulse(isLeftArm);
	}

	private void UpdateMovementAimError(bool hasAimedArm)
	{
		float deltaTime = Mathf.Max(GetActiveRagdollDeltaTime(), 0.0001f);
		if (!ShouldUseMovementAimError(hasAimedArm))
		{
			SettleMovementAimError(deltaTime);
			return;
		}

		float speed = GetMovementAimErrorSpeed();
		float speedT = GetMovementAimErrorSpeedPercent(speed, movementAimErrorMinSpeed, movementAimErrorSpeedForMax);
		if (speedT <= 0f)
		{
			SettleMovementAimError(deltaTime);
			return;
		}

		float coneDegrees = Mathf.Max(0f, movementAimErrorMaxDegrees) * speedT;
		movementAimErrorRetargetTimer -= deltaTime;
		if (movementAimErrorRetargetTimer <= 0f)
		{
			targetMovementAimError = UnityEngine.Random.insideUnitCircle * coneDegrees;
			movementAimErrorRetargetTimer = Mathf.Max(0.01f, movementAimErrorRetargetInterval);
		}
		else
		{
			targetMovementAimError = Vector2.ClampMagnitude(targetMovementAimError, coneDegrees);
		}

		SmoothMovementAimError(targetMovementAimError, movementAimErrorFollowSpeed, deltaTime);
	}

	private void UpdateMovementShotError(bool hasAimedArm)
	{
		float deltaTime = Mathf.Max(GetActiveRagdollDeltaTime(), 0.0001f);
		if (!ShouldUpdateMovementShotError(hasAimedArm))
		{
			SettleMovementShotError(deltaTime);
			return;
		}

		SettleMovementShotError(deltaTime);
	}

	private bool ShouldUpdateMovementShotError(bool hasAimedArm)
	{
		return enableMovementAimError
			&& hasAimedArm
			&& bodyController != null
			&& ownerIsPlayer
			&& !bodyController.isAI
			&& rb != null;
	}

	private void SettleMovementAimError(float deltaTime)
	{
		targetMovementAimError = Vector2.zero;
		movementAimErrorRetargetTimer = 0f;
		SmoothMovementAimError(Vector2.zero, movementAimErrorSettleSpeed, deltaTime);
	}

	private void SmoothMovementAimError(Vector2 targetError, float speed, float deltaTime)
	{
		float t = Mathf.Clamp01(1f - Mathf.Exp(-Mathf.Max(0f, speed) * deltaTime));
		currentMovementAimError = Vector2.Lerp(currentMovementAimError, targetError, t);
		if (currentMovementAimError.sqrMagnitude <= 0.0001f)
		{
			currentMovementAimError = Vector2.zero;
			movementAimErrorRotation = Quaternion.identity;
			return;
		}

		movementAimErrorRotation = Quaternion.Euler(currentMovementAimError.y, currentMovementAimError.x, 0f);
	}

	private void SettleMovementShotError(float deltaTime)
	{
		float t = Mathf.Clamp01(1f - Mathf.Exp(-Mathf.Max(0f, movementShotErrorDecaySpeed) * deltaTime));
		currentMovementShotError = Vector2.Lerp(currentMovementShotError, Vector2.zero, t);
		UpdateMovementShotErrorRotation();
	}

	private void UpdateMovementShotErrorRotation()
	{
		if (currentMovementShotError.sqrMagnitude <= 0.0001f)
		{
			currentMovementShotError = Vector2.zero;
			movementShotErrorRotation = Quaternion.identity;
			return;
		}

		movementShotErrorRotation = Quaternion.Euler(currentMovementShotError.y, currentMovementShotError.x, 0f);
	}

	private bool ShouldUseMovementAimError(bool hasAimedArm)
	{
		return enableMovementAimError
			&& hasAimedArm
			&& bodyController != null
			&& ownerIsPlayer
			&& !bodyController.isAI
			&& rb != null
			&& movementAimErrorMaxDegrees > 0f;
	}

	private bool ShouldUseMovementAimShotError(bool isLeftArm)
	{
		if (!enableMovementAimError
			|| bodyController == null
			|| bodyState == null
			|| !ownerIsPlayer
			|| bodyController.isAI
			|| bodyController.isDead
			|| bodyState.hitStunAmount > 0f
			|| rb == null
			|| movementShotErrorMaxDegrees <= 0f)
		{
			return false;
		}

		return isLeftArm ? bodyController.IsLeftArmAimed : bodyController.IsRightArmAimed;
	}

	private float GetMovementAimErrorSpeed()
	{
		Vector3 velocity = rb.velocity;
		velocity.y = 0f;
		return velocity.magnitude;
	}

	private float GetMovementAimErrorSpeedPercent(float speed, float minSpeedValue, float speedForMaxValue)
	{
		float minSpeed = Mathf.Max(0f, minSpeedValue);
		float maxSpeed = Mathf.Max(minSpeed + 0.0001f, speedForMaxValue);
		return Mathf.Clamp01((speed - minSpeed) / (maxSpeed - minSpeed));
	}

	private Quaternion ApplyMovementAimError(Quaternion baseRotation, bool applyMovementAimError)
	{
		if (!applyMovementAimError)
		{
			return baseRotation;
		}

		return baseRotation * movementAimErrorRotation * movementShotErrorRotation;
	}

	private void UpdateArmAimForShotImpulse(bool isLeftArm)
	{
		if (isLeftArm)
		{
			if (leftAssistedAimTravel.active)
			{
				return;
			}
			UpdateArmAim(RagdollLeftArm, RagdollLeftWeapon, targetL, bodyController.gunsL, armAimFollowSpeed, true, true);
			return;
		}

		if (rightAssistedAimTravel.active)
		{
			return;
		}
		UpdateArmAim(RagdollRightArm, RagdollRightWeapon, target, bodyController.guns, armAimFollowSpeed, true, true);
	}

	private float GetAimFollowT(float followSpeed)
	{
		followSpeed = Mathf.Max(0f, followSpeed);
		return Mathf.Clamp01(1f - Mathf.Exp(-followSpeed * GetActiveRagdollDeltaTime()));
	}

	private BulletTimeChannel GetActiveRagdollChannel()
	{
		return ownerIsPlayer ? BulletTimeChannel.PlayerActiveRagdoll : BulletTimeChannel.EnemyHitReaction;
	}

	private float GetActiveRagdollScale()
	{
		return BulletTimeManager.GetScale(GetActiveRagdollChannel());
	}

	private float GetActiveRagdollDeltaTime()
	{
		return BulletTimeManager.GetDeltaTime(GetActiveRagdollChannel());
	}

	private bool TryGetAimRotation(Vector3 direction, Vector3 up, Quaternion fallback, out Quaternion rotation)
	{
		if (direction.sqrMagnitude <= minAimDirectionSqrMagnitude)
		{
			rotation = fallback;
			return false;
		}

		if (up.sqrMagnitude <= minAimDirectionSqrMagnitude)
		{
			up = Vector3.up;
		}

		rotation = Quaternion.LookRotation(direction, up);
		return true;
	}

	private void UpdateJointTargets()
	{
		for (int i = 0; i < bonesAndJoints.Length; i++)
		{
			if ((i == 8 || i == 10 || i == 6 || i == 7) && rb.velocity.magnitude < 0.1f)
			{
				ConfigurableJointExtensions.SetTargetRotationLocal(bonesAndJoints[i].joint, bonesAndJoints[i].bone.localRotation, _initialJointsRotation[i]);
			}
			else if (i != 8 || i != 10 || i != 6 || i != 7)
			{
				ConfigurableJointExtensions.SetTargetRotationLocal(bonesAndJoints[i].joint, bonesAndJoints[i].bone.localRotation, _initialJointsRotation[i]);
			}
		}
	}
}
