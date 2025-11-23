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

public class ActiveRagdollController : MonoBehaviour
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
			RagdollHead.AddForce(currentVelocity * headStabilizerForce, ForceMode.Acceleration);
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

		RagdollSpineLowerRb.AddTorque(new Vector3(0, rot.y, 0)
															* uprightTorque * balancePercent);
		// RagdollSpineLowerRb.AddTorque(new Vector3(rot.x, rot.y, rot.z)
		// 											* uprightTorque * balancePercent);

		var directionAnglePercent = Vector3.SignedAngle(RagdollSpineLowerRb.transform.forward,
							TargetDirection, Vector3.up) / 180;
		RagdollSpineLowerRb.AddRelativeTorque(0, directionAnglePercent * rotationTorque, 0);

		if (RagdollSpineLowerRb.position.y < 1.31f)
		{
			RagdollSpineLowerRb.AddForce(new Vector3(0, upwardStabilizerForce, 0), ForceMode.Acceleration);

			// RagdollSpineLowerRb.AddForce(new Vector3(0, 7000, 0), ForceMode.Force);
		}
		else if (RagdollSpineLowerRb.position.y < 1.32f)
		{
			RagdollSpineLowerRb.AddForce(new Vector3(0, -downwardStabilizerForce, 0), ForceMode.Acceleration);
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
		float speedR = (1f / RagdollRightArmRb.velocity.magnitude);
		Vector3 direction = target.transform.position - RagdollRightArm.transform.position;
		Quaternion toRotation = Quaternion.LookRotation(direction, transform.up);
		//AnimatedRightArm.transform.rotation = toRotation;
		//AnimatedRightWeapon.transform.rotation = toRotation;
		//RagdollRightWeapon.transform.rotation = Quaternion.Lerp(RagdollRightWeapon.transform.rotation, toRotation, speed * Time.deltaTime);
		//RagdollRightArm.transform.rotation = Quaternion.Lerp(RagdollRightArm.transform.rotation, toRotation, speed * Time.deltaTime);
		//RagdollRightArm.transform.rotation = toRotation;

		RagdollRightArm.transform.rotation = Quaternion.Lerp(RagdollRightArm.transform.rotation, toRotation, speedR * Time.deltaTime);

		Vector3 d = target.transform.position - RagdollRightWeapon.transform.position;
		Quaternion t = Quaternion.LookRotation(d, transform.right);

		RagdollRightWeapon.transform.rotation = Quaternion.Lerp(RagdollRightWeapon.transform.rotation, t, speedR * Time.deltaTime);


		float speedL = (1f / RagdollLeftArmRb.velocity.magnitude);
		direction = targetL.transform.position - RagdollLeftArm.transform.position;
		toRotation = Quaternion.LookRotation(direction, transform.up);

		RagdollLeftArm.transform.rotation = Quaternion.Lerp(RagdollLeftArm.transform.rotation, toRotation, speedL * Time.deltaTime);

		d = targetL.transform.position - RagdollLeftWeapon.transform.position;
		t = Quaternion.LookRotation(d, transform.right);

		RagdollLeftWeapon.transform.rotation = Quaternion.Lerp(RagdollLeftWeapon.transform.rotation, t, speedL * Time.deltaTime);

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
