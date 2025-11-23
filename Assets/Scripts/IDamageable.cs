using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DamageInfo
{
	public float amount;
	public float impactForce;
	public Vector3 impactVector;
	public Limb limb;
	public bool BypassShields { get; set; } = false;
	public float ChanceToStartFire { get; set; } = 0.0f;

	public DamageInfo(float amount)
	{
		this.amount = amount;
	}
}

public interface IDamageable
{
	void TakeDamage(DamageInfo i);
}
