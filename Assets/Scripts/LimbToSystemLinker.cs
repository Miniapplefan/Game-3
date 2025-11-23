using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static BodyInfo;

[System.Serializable]
public struct Limb
{
	public enum LimbID
	{
		none,
		leftLeg,
		rightLeg,
		torso,
		rightArm,
		head
	}

	public systemID linkedSystem;
	public LimbID specificLimb;

}

public class LimbToSystemLinker : MonoBehaviour, IDamageable
{
	public Limb limb;
	//public systemID linkedSystem;
	private BodyController controller;

	// Start is called before the first frame update
	void Start()
	{
		controller = GetComponentInParent<BodyController>();
	}

	public void TakeDamage(DamageInfo i)
	{
		i.limb = limb;
		controller.HandleDamage(i);
	}
}
