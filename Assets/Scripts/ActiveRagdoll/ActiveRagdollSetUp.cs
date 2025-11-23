// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// ActiveRagdollSetUp
using UnityEngine;

public class ActiveRagdollSetUp : MonoBehaviour
{
	public Transform leftFoot;

	public Transform rightFoot;

	public Transform rootBone;

	private void Start()
	{
		setUp();
	}


	public void setUp()
	{
		if (leftFoot == null || rightFoot == null || rootBone == null)
		{
			Debug.LogError("Set The Paramaters");
			return;
		}
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
		transform3.position = new Vector3(transform.position.x, rootBone.position.y, transform.position.z + 1f);
		transform4.position = new Vector3(transform2.position.x, rootBone.position.y, transform2.position.z + 1f);
		transform3.parent = rootBone;
		transform4.parent = rootBone;
		InverseKinematics inverseKinematics = leftFoot.gameObject.AddComponent<InverseKinematics>();
		InverseKinematics inverseKinematics2 = rightFoot.gameObject.AddComponent<InverseKinematics>();
		inverseKinematics.Target = transform;
		inverseKinematics2.Target = transform2;
		inverseKinematics.Pole = transform3;
		inverseKinematics2.Pole = transform4;
		ActiveRagdoll activeRagdoll = base.gameObject.AddComponent<ActiveRagdoll>();
		activeRagdoll.rootBone = rootBone;
		activeRagdoll.moveDir = new GameObject("Move Direction").transform;
		activeRagdoll.moveDir.parent = base.transform;
		activeRagdoll.moveDir.position = rootBone.position;
		activeRagdoll.leftFoot = transform;
		activeRagdoll.rightFoot = transform2;
		Object.DestroyImmediate(this);
	}
}
