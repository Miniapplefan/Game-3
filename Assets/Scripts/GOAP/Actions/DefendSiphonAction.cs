using System;
using System.Linq;
using System.Collections.Generic;
using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Enums;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;

public class DefendSiphonAction : ActionBase<AttackData>, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] Colliders = new Collider[1];

	public enum LimbToTarget { Head, RightArm, RightLeg, LeftLeg };

	public struct LimbConsideration
	{
		public LimbToTarget limb;
		public Func<float> considerationFunction;

		public LimbConsideration(LimbToTarget l, Func<float> considerationFunction)
		{
			this.limb = l;
			this.considerationFunction = considerationFunction;
		}

		public float Consideration()
		{
			return considerationFunction();
		}
	}

	public List<LimbConsideration> limbConsiderations;


	public struct GunConsideration
	{
		public int gunSlot;
		public Gun gun;

		//public Func<float> considerationFunction;

		public GunConsideration(int i, Gun g)
		{
			this.gunSlot = i;
			this.gun = g;
			//this.considerationFunction = considerationFunction;
		}

		// public float Consideration()
		// {
		// 	return considerationFunction();
		// }
	}

	public List<GunConsideration> gunConsideration;
	public GunConsideration topRankedGun;

	public override void Start(IMonoAgent agent, AttackData data)
	{
		data.Timer = AttackConfig.AttackDelay;
		limbConsiderations = new List<LimbConsideration>();
		limbConsiderations.Add(new LimbConsideration(LimbToTarget.Head, () => ConsiderHead(agent, data)));
		limbConsiderations.Add(new LimbConsideration(LimbToTarget.RightArm, () => ConsiderRightArm(agent, data)));
		limbConsiderations.Add(new LimbConsideration(LimbToTarget.RightLeg, () => ConsiderLegs(agent, data)));
		limbConsiderations.Add(new LimbConsideration(LimbToTarget.LeftLeg, () => ConsiderLegs(agent, data)));

		gunConsideration = new List<GunConsideration>();
		gunConsideration.Add(new GunConsideration(0, data.bodyState.weapons.gunSelector.ActiveGun1));
		gunConsideration.Add(new GunConsideration(1, data.bodyState.weapons.gunSelector.ActiveGun2));
		gunConsideration.Add(new GunConsideration(2, data.bodyState.weapons.gunSelector.ActiveGun3));


	}
	public override void Created()
	{
	}

	public override ActionRunState Perform(IMonoAgent agent, AttackData data, ActionContext context)
	{
		data.Timer -= context.DeltaTime;
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			// if (data.targetState != null && data.targetState.bodyIsOverheated)
			// {
			// 	data.AIController.SetAimTarget(data.targetState.head.transform.position);
			// }

			if (data.targetState != null)
			{
				List<LimbConsideration> targetableLimbs = sortLimbsToTarget(agent, data);
				foreach (var limbConsideration in targetableLimbs)
				{
					Vector3 limbPos = LimbToPosition(limbConsideration.limb, data);
					if (!IsLimbObstructed(limbPos, agent))
					{
						if ((limbConsideration.limb == LimbToTarget.LeftLeg && data.targetState.legs.leftLegHealth == 0) || (limbConsideration.limb == LimbToTarget.RightLeg && data.targetState.legs.rightLegHealth == 0))
						{
							continue;
						}
						//Debug.Log(limbConsideration.limb);
						data.AIController.SetAimTarget(limbPos);
					}
				}

				List<GunConsideration> shootableGuns = rankGunToUse(data, agent);
				//Debug.Log("I want to fire " + data.bodyState.weapons.guns[topRankedGun.gunSlot].gunData.GunName);
				foreach (var gunConsideration in shootableGuns)
				{
					if (data.bodyState.weapons.guns[gunConsideration.gunSlot].isCharged() && data.bodyState.weapons.GetCurrentPowerAllocationDictionary()[topRankedGun.gunSlot])
					{
						//Debug.Log("Able to fire");
						data.bodyState.desiredGunToUse = gunConsideration.gun;
						topRankedGun = gunConsideration;
						break;
					}
					else
					{
						//Debug.Log("Stopping Here");
						return ActionRunState.Stop;
					}
				}

			}
			else
			{
				data.AIController.SetAimTarget(Colliders[0].transform.position);
				topRankedGun = gunConsideration[0];
				data.bodyState.desiredGunToUse = topRankedGun.gun;
			}

			// if (data.targetState != null && data.targetState.bodyIsOverheated)
			// {
			// 	data.AIController.SetAimTarget(data.targetState.head.bounds.center);
			// }
			// else
			// {
			// 	data.AIController.SetAimTarget(Colliders[0].transform.position);
			// 	//data.AIController.SetAimTarget(data.targetState.head.transform.position);
			// }
		}

		//data.bodyState.positionTracker.transform.position = agent.GetComponentInChildren<BodyState>().head.transform.position;
		//data.bodyState.positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
		float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);
		Vector3 direction1 = (Colliders[0].transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
		RaycastHit hit1;
		bool seePlayer = false;
		if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
		{
			//data.bodyState.positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.red;
			// Debug.Log(agent.transform.position);
			if (hit1.transform.GetComponent<PlayerController>() != null)
			{
				seePlayer = true;
				data.targetState = hit1.transform.GetComponent<BodyState>();
				data.bodyState.targetBodyState = data.targetState;
			}
		}

		bool shouldAttack = seePlayer
		&& data.bodyState.weapons.weaponRb.angularVelocity.magnitude < 0.5f
		&& data.navMeshAgent.velocity.magnitude < 0.05f
		&& !data.bodyState.Weapons_currentlyFiring()
		&& distanceToPlayer <= topRankedGun.gun.gunData.shootConfig.maxRange
		;
		if (shouldAttack)
		{
			//Debug.Log("Attacking");
			// if (data.targetState.bodyIsOverheated)
			// {
			// 	data.AIController.SetAimTarget(data.targetState.head.transform.position);
			// }

			//Debug.Log("Firing Weapon");
			// Debug.Log(data.bodyState.Weapons_weapon1Powered());
			// while (!data.bodyState.Weapons_weapon1Powered())
			// {
			//   data.AIController.didScroll = true;
			// }

			if (data.bodyState.Weapons_currentlyFiringBurst() || data.bodyState.Weapons_currentlyFiring())
			{
				return ActionRunState.Continue;
			}

			if (data.bodyState.weapons.guns[topRankedGun.gunSlot].isCharged() && data.bodyState.weapons.GetCurrentPowerAllocationDictionary()[topRankedGun.gunSlot])
			{
				if (topRankedGun.gunSlot == 0)
				{
					data.AIController.pressingFire1 = true;
					return ActionRunState.Continue;
				}
				else if (topRankedGun.gunSlot == 1)
				{
					data.AIController.pressingFire2 = true;
					return ActionRunState.Continue;
				}
				else if (topRankedGun.gunSlot == 2)
				{
					data.AIController.pressingFire3 = true;
					return ActionRunState.Continue;
				}
			}
			// if (data.bodyState.Weapons_weapon1Charged() && data.bodyState.Weapons_weapon1Powered())
			// {
			// 	data.AIController.pressingFire1 = true;
			// 	return ActionRunState.Continue;
			// }
			// else if (data.bodyState.Weapons_weapon2Charged() && data.bodyState.Weapons_weapon2Powered())
			// {
			// 	data.AIController.pressingFire2 = true;
			// 	return ActionRunState.Continue;
			// }
			// else if (data.bodyState.Weapons_weapon3Charged() && data.bodyState.Weapons_weapon3Powered())
			// {
			// 	data.AIController.pressingFire3 = true;
			// 	return ActionRunState.Continue;
			// }
			else
			{
				//data.bodyState.weapons.CycleToNextPowerAllocationDictionary();
				return ActionRunState.Stop;
			}
		}
		// else
		// {
		// 	return ActionRunState.Stop;
		// }
		return data.Timer > 0 ? ActionRunState.Continue : ActionRunState.Stop;
	}

	private Vector3 LimbToPosition(LimbToTarget limb, AttackData data)
	{
		switch (limb)
		{
			case LimbToTarget.Head:
				return data.targetState.headCollider.bounds.center;
			case LimbToTarget.RightArm:
				return Colliders[0].transform.position;
			case LimbToTarget.RightLeg:
				return data.targetState.rightLeg.bounds.center;
			case LimbToTarget.LeftLeg:
				return data.targetState.leftLeg.bounds.center;
			default:
				return data.targetState.rightArm.bounds.center;
		}
	}

	private bool IsLimbObstructed(Vector3 limbPos, IMonoAgent agent)
	{
		Vector3 directionToLimb = (limbPos - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
		float distanceToLimb = Vector3.Distance(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, limbPos);
		RaycastHit hit;
		if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, directionToLimb, out hit, distanceToLimb, AttackConfig.ObstructionLayerMask))
		{
			// Limb is obstructed if something was hit
			return true;
		}
		return false;
	}

	private List<LimbConsideration> sortLimbsToTarget(IMonoAgent agent, AttackData data)
	{
		// Sort the limb considerations by their utility in descending order
		List<LimbConsideration> sortedLimbConsiderations = limbConsiderations
				.OrderByDescending(lc => lc.Consideration())
				.ToList();

		return sortedLimbConsiderations;
	}

	public float ConsiderHead(IMonoAgent agent, AttackData data)
	{
		float hostileOverheated = data.targetState.head.health < 4 ? 0.6f : 0;

		return hostileOverheated;
	}

	public float ConsiderRightArm(IMonoAgent agent, AttackData data)
	{
		// if (data.targetState.bodyIsOverheated && data.targetState.siphon.extended)
		// {
		// 	return 1;
		// }

		return 0.5f;
	}

	public float ConsiderLegs(IMonoAgent agent, AttackData data)
	{
		if (data.targetState.bodyIsOverheated && data.targetState.legs.getBaseSpeed() > 0)
		{
			return 1;
		}

		float hostileTagging = data.targetState.Legs_getTaggingHealth() / 100;

		return hostileTagging;
	}

	private List<GunConsideration> rankGunToUse(AttackData data, IMonoAgent agent)
	{
		if (data.targetState.Legs_getTaggingHealth() / 100 > 0.5 && (!IsLimbObstructed(LimbToPosition(LimbToTarget.RightLeg, data), agent) || (!IsLimbObstructed(LimbToPosition(LimbToTarget.LeftLeg, data), agent))))
		{
			List<GunConsideration> sortedGunConsiderations = gunConsideration
				.OrderByDescending(lc => lc.gun.gunData.shootConfig.impactForce * gunRankRangeConsideration(data, agent, lc.gun))
				.ToList();

			// foreach (var gc in sortedGunConsiderations)
			// {
			// 	Debug.Log(gc.gun.name + " " + gc.gun.gunData.shootConfig.impactForce * gunRankRangeConsideration(data, agent, gc.gun));
			// }

			return sortedGunConsiderations;
		}
		else if (data.targetState.HeatContainer_getCurrentHeat() / data.targetState.cooling.GetMaxHeat() > 0.9f)
		{
			List<GunConsideration> sortedGunConsiderations = gunConsideration
				.OrderByDescending(lc => lc.gun.gunData.shootConfig.bulletsPerShot * lc.gun.gunData.shootConfig.burst_numShots * gunRankRangeConsideration(data, agent, lc.gun))
				.ToList();

			// foreach (var gc in sortedGunConsiderations)
			// {
			// 	Debug.Log(gc.gun.name + " " + gc.gun.gunData.shootConfig.impactForce * gunRankRangeConsideration(data, agent, gc.gun));
			// }

			return sortedGunConsiderations;
		}
		else
		{
			List<GunConsideration> sortedGunConsiderations = gunConsideration
				.OrderByDescending(lc => lc.gun.gunData.shootConfig.heatPerShot * gunRankRangeConsideration(data, agent, lc.gun))
				.ToList();

			// foreach (var gc in sortedGunConsiderations)
			// {
			// 	Debug.Log(gc.gun.name + " " + gc.gun.gunData.shootConfig.impactForce * gunRankRangeConsideration(data, agent, gc.gun));
			// }

			return sortedGunConsiderations;
		}

	}

	private float gunRankRangeConsideration(AttackData data, IMonoAgent agent, Gun gun)
	{
		//Debug.Log(gun.name + " " + (Mathf.Abs(Vector3.Distance(data.targetState.rb.transform.position, agent.transform.position)) <= gun.gunData.shootConfig.maxRange || agent.GetComponentInChildren<BodyState>().legs.getMoveSpeed() > 0 ? 1 : 0));
		return Mathf.Abs(Vector3.Distance(data.targetState.rb.transform.position, agent.transform.position)) <= gun.gunData.shootConfig.maxRange || agent.GetComponentInChildren<BodyState>().legs.getMoveSpeed() > 0 ? 1 : 0;
	}

	public override void End(IMonoAgent agent, AttackData data)
	{
		data.AIController.pressingFire1 = false;
		data.AIController.pressingFire2 = false;
		data.AIController.pressingFire3 = false;
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}