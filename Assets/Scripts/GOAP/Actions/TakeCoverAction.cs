using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Enums;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;

public class TakeCoverAction : ActionBase<CommonData>, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] Colliders = new Collider[1];


	public override void Created() { }

	public override void Start(IMonoAgent agent, CommonData data)
	{
		data.Timer = Random.Range(2, 3);
	}

	public override ActionRunState Perform(IMonoAgent agent, CommonData data, ActionContext context)
	{
		data.Timer -= context.DeltaTime;

		// if (data.Timer > 0)
		// {
		//   return ActionRunState.Continue;
		// }

		// if (data.bodyState.isAimed)
		// {
		// 	data.bodyState.isAimed = false;
		// 	data.bodyState.TimeToAim = AttackConfig.TimeToAim;
		// }

		bool seePlayer = false;
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			if (data.bodyState.targetBodyState == null)
			{
				foreach (Collider c in Colliders)
				{
					if (c.gameObject.GetComponentInChildren<PlayerController>() != null)
					{
						data.bodyState.targetBodyState = c.gameObject.GetComponent<BodyState>();
					}
				}
			}

			Vector3 direction1 = (data.bodyState.targetBodyState.transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
			RaycastHit hit1;
			if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
			{
				//Debug.Log(agent.transform.position);
				seePlayer = hit1.transform.GetComponentInChildren<PlayerController>() != null;
			}
		}

		if (data.bodyState.dangerLevel < 0.1f) return ActionRunState.Stop;

		if (seePlayer)
		{
			return ActionRunState.Continue;
		}
		else
		{
			return ActionRunState.Stop;
		}

		// if (!seePlayer)
		// {
		// 	// Debug.Log("See player");
		// 	return ActionRunState.Stop;
		// }
		// else
		// {
		// 	Debug.Log("Do not see player");
		// }

		// if (data.bodyState.heatContainer.GetTemperatureRelativeToAir() < 1)
		// {
		// 	return ActionRunState.Stop;
		// }

	}

	public override void End(IMonoAgent agent, CommonData data) { }
	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}