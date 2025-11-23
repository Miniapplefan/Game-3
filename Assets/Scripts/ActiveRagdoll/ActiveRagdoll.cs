// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// ActiveRagdoll
using System.Collections;
using UnityEngine;

public class ActiveRagdoll : MonoBehaviour
{
	[Tooltip("The root bone")]
	public Transform rootBone;

	[Tooltip("This object shows the direction the body is moving in.")]
	public Transform moveDir;

	[Header("IK targets")]
	public Transform leftFoot;

	public Transform rightFoot;

	[Header("standing position values")]
	[Tooltip("The amount of space between the feet")]
	public float footGap = 0.2f;

	[Tooltip("The height of the body relative to the ground")]
	public float standHeight = 10f;

	[Header("step values")]
	[Tooltip("The minimum distance away from the body the foot must be to take a step")]
	public float stepDist = 0.7f;

	[Tooltip("The distance the foot travels during a step")]
	public float stepSize = 0.8f;

	[Tooltip("The amount of time it will take to take a step. The lower the value the faster the feet will move")]
	public float stepDuration = 0.3f;

	[Tooltip("How high the foot will go off the ground while taking a step")]
	public float stepHeight = 1.5f;

	[Header("physics")]
	[Tooltip("This doesnt matter")]
	public bool physics;

	[Tooltip("The force applied to keep the ragdoll standing upright. The higher it is the harder it is to knock the ragdoll over")]
	public int rotationForce = 500;

	public int rotationSmoothness = 1;

	[Tooltip("Add a layer called ragdolls and apply it to this object and all its children")]
	public LayerMask ragdollLayer = 64;

	private Vector3 lastFrame;

	private Transform leftSide;

	private Transform rightSide;

	private Vector3 leftTarget;

	private Vector3 rightTarget;

	[HideInInspector]
	public ConfigurableJoint joint;

	[HideInInspector]
	public Rigidbody connectedBody;

	private Rigidbody rb;

	[HideInInspector]
	public bool falling;

	public AnimationCurve uprightTorqueFunction;
	public float uprightTorque = 10000000;
	public float rotationTorque = 10000000;
	public Vector3 TargetDirection { get; set; }

	private void Start()
	{
		moveDir.position = rootBone.position;
		lastFrame = rootBone.position;
		rb = rootBone.GetComponent<Rigidbody>();
		connectedBody = new GameObject("joint").AddComponent<Rigidbody>();
		connectedBody.transform.parent = base.transform;
		connectedBody.isKinematic = true;
		setupJoint();
		leftSide = new GameObject("left side").transform;
		rightSide = new GameObject("right side").transform;
		leftSide.parent = moveDir;
		rightSide.parent = moveDir;
		leftSide.localPosition = new Vector3(footGap, 0f, 0f);
		rightSide.localPosition = new Vector3(0f - footGap, 0f, 0f);
	}

	private void Update()
	{
		setMoveDir();
		Physics.Raycast(leftSide.position, Vector3.down, out var hitInfo, 5f, ~(int)ragdollLayer);
		Physics.Raycast(rightSide.position, Vector3.down, out var hitInfo2, 5f, ~(int)ragdollLayer);
		if (hitInfo.collider == null || hitInfo2.collider == null)
		{
			falling = true;
		}
		else
		{
			falling = false;
		}
		float num = Vector3.Distance(hitInfo.point, leftTarget);
		float num2 = Vector3.Distance(hitInfo2.point, rightTarget);

		if (num >= stepDist && num2 >= stepDist)
		{
			if (num2 > num)
			{
				rightTarget = hitInfo.point;
				StartCoroutine(step(rightFoot, rightTarget));
			}
			else
			{
				leftTarget = hitInfo2.point;
				StartCoroutine(step(leftFoot, leftTarget));
			}
		}
		leftFoot.eulerAngles = new Vector3(leftFoot.eulerAngles.x, rootBone.eulerAngles.y, leftFoot.eulerAngles.z);
		rightFoot.eulerAngles = new Vector3(rightFoot.eulerAngles.x, rootBone.eulerAngles.y, rightFoot.eulerAngles.z);
		moveDir.eulerAngles = new Vector3(moveDir.eulerAngles.x, rootBone.eulerAngles.y, moveDir.eulerAngles.z);
		if (!falling)
		{
			//(hitInfo.point.y + hitInfo.point.y) / 2f + standHeight
			connectedBody.transform.position = new Vector3(rootBone.position.x, standHeight, rootBone.position.z);
		}
		ApplyUprightTorque();
	}

	private void ApplyUprightTorque()
	{
		var balancePercent = Vector3.Angle(rb.transform.up,
														 Vector3.up) / 180;
		//balancePercent = uprightTorqueFunction.Evaluate(balancePercent);
		var rot = Quaternion.FromToRotation(rb.transform.up,
											 Vector3.up).normalized;

		// rb.AddTorque(new Vector3(rot.x, rot.y, rot.z)
		// 											* uprightTorque * balancePercent);

		rb.AddTorque(new Vector3(rot.x, rot.y, rot.z)
													* uprightTorque);

		var directionAnglePercent = Vector3.SignedAngle(rb.transform.forward,
							TargetDirection, Vector3.up) / 180;
		rb.AddRelativeTorque(0, directionAnglePercent * rotationTorque, 0);
	}

	private IEnumerator step(Transform foot, Vector3 target)
	{
		Vector3 startPoint = foot.position;
		Vector3 centerPoint = (foot.position + target) / 2f;
		centerPoint.y = target.y + stepHeight;
		float timeElapsed = 0f;
		do
		{
			timeElapsed += Time.deltaTime;
			float t = timeElapsed / stepDuration;
			foot.position = Vector3.Lerp(Vector3.Lerp(startPoint, centerPoint, t), Vector3.Lerp(centerPoint, target, t), t);
			yield return null;
		}
		while (timeElapsed < stepDuration);
	}

	public void setupJoint()
	{
		if (joint != null)
		{
			Object.Destroy(joint);
		}
		joint = rootBone.gameObject.AddComponent<ConfigurableJoint>();
		JointDrive angularXDrive = joint.angularXDrive;
		JointDrive angularYZDrive = joint.angularYZDrive;
		JointDrive yDrive = joint.yDrive;
		angularXDrive.positionSpring = rotationForce;
		angularXDrive.positionDamper = rotationSmoothness;
		angularYZDrive.positionSpring = rotationForce;
		angularYZDrive.positionDamper = rotationSmoothness;
		yDrive.positionSpring = 1000000f;
		yDrive.positionDamper = 10f;
		joint.angularXDrive = angularXDrive;
		joint.angularYZDrive = angularYZDrive;
		joint.yDrive = yDrive;
		connectedBody.transform.position = new Vector3(rootBone.position.x, rootBone.position.y, rootBone.position.z);
		connectedBody.transform.rotation = rootBone.rotation;
		//joint.connectedBody = connectedBody;
		connectedBody.transform.rotation = Quaternion.identity;
	}

	public void setMoveDir()
	{
		if (physics)
		{
			if ((double)rb.velocity.magnitude > 0.3)
			{
				Vector3 velocity = rb.velocity;
				velocity.Normalize();
				velocity.y = 0f;
				moveDir.position = rootBone.position + velocity * stepSize;
			}
			else
			{
				moveDir.position = rootBone.position;
			}
			return;
		}
		if ((double)Vector3.Distance(lastFrame, rootBone.position) > 0.005)
		{
			Vector3 vector = rootBone.position - lastFrame;
			vector.Normalize();
			vector.y = 0f;
			moveDir.position = rootBone.position + vector * stepSize;
		}
		else
		{
			moveDir.position = rootBone.position;
		}
		lastFrame = rootBone.position;
	}
}
