// Assembly-CSharp, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// LegsLimits
using UnityEngine;

public class LegsLimits : MonoBehaviour
{
	public Ragdoll ragdoll;

	private bool ragdolled;

	private ConfigurableJoint joint;

	private ConfigurableJointMotion xMotion;

	private ConfigurableJointMotion yMotion;

	private ConfigurableJointMotion zMotion;

	private void Start()
	{
		joint = GetComponent<ConfigurableJoint>();
		xMotion = joint.angularXMotion;
		yMotion = joint.angularYMotion;
		zMotion = joint.angularZMotion;
		setLimits();
	}

	private void Update()
	{
		if (ragdolled != ragdoll.ragdolled)
		{
			setLimits();
		}
		ragdolled = ragdoll.ragdolled;
	}

	private void setLimits()
	{
		if (ragdoll.ragdolled)
		{
			joint.angularXMotion = xMotion;
			joint.angularYMotion = yMotion;
			joint.angularZMotion = zMotion;
		}
		else
		{
			joint.angularXMotion = ConfigurableJointMotion.Free;
			joint.angularYMotion = ConfigurableJointMotion.Free;
			joint.angularZMotion = ConfigurableJointMotion.Free;
		}
	}
}
