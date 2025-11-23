// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// Ragdoll
using System.Collections;
using UnityEngine;

public class Ragdoll : MonoBehaviour
{
	public ActiveRagdoll walkScript;

	[Tooltip("The InversKinematics script of the feet bones")]
	public InverseKinematics leftIk;

	[Tooltip("The InversKinematics script of the feet bones")]
	public InverseKinematics rightIk;

	[Tooltip("The rigidbody attached to the root bone")]
	public Rigidbody hipsRb;

	[Tooltip("The higher the angle is the harder it will be to knock over the ragdoll and the better its balance will be.")]
	public int fallAngle = 100;

	[HideInInspector]
	public bool ragdolled;

	private bool conscious = true;

	private void Start()
	{
	}

	private void Update()
	{
		if (!ragdolled && conscious)
		{
			if (Vector3.Angle(hipsRb.transform.up, Vector3.up) > (float)fallAngle)
			{
				ragdoll();
			}
			else if (walkScript.falling)
			{
				ragdoll();
			}
		}
		if (ragdolled && conscious)
		{
			if ((double)hipsRb.velocity.magnitude < 0.1)
			{
				ragdoll();
			}
			else if (hipsRb.velocity.magnitude < 1f)
			{
				StartCoroutine(setConscious(3f));
			}
		}
	}

	public void ragdoll()
	{
		if (!ragdolled)
		{
			Object.Destroy(walkScript.joint);
			hipsRb.useGravity = true;
			walkScript.enabled = false;
			leftIk.enabled = false;
			rightIk.enabled = false;
			ragdolled = true;
			StartCoroutine(setConscious(5f));
		}
		else
		{
			walkScript.enabled = true;
			leftIk.enabled = true;
			rightIk.enabled = true;
			walkScript.setupJoint();
			hipsRb.useGravity = false;
			ragdolled = false;
			StartCoroutine(setConscious(3f));
		}
	}

	public IEnumerator setConscious(float time)
	{
		conscious = false;
		yield return new WaitForSeconds(time);
		conscious = true;
	}
}
