using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Enums;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;

public class CooldownAction : ActionBase<CommonData>, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] Colliders = new Collider[1];
	private Collider[] EnvironmentalCoolingColliders = new Collider[10];


	public override void Created() { }

	public override void Start(IMonoAgent agent, CommonData data)
	{
		data.Timer = Random.Range(1, 2);
	}

	public override ActionRunState Perform(IMonoAgent agent, CommonData data, ActionContext context)
	{
		data.Timer -= context.DeltaTime;

		// if (data.Timer > 0)
		// {
		// 	return ActionRunState.Continue;
		// }
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);
			if (distanceToPlayer < 6.0f)
			{
				return ActionRunState.Stop;
			}
		}
		var bodyState = agent.GetComponentInChildren<BodyState>();
		var ag = bodyState.heatContainer.airGrid;
		bool posTempCheck = ag.GetTemperature(ag.WorldToVoxel(agent.transform.position)) < bodyState.heatContainer.currentTemperature ? true : false;

		if (!posTempCheck)
		{
			return ActionRunState.Stop;
		}

		//Determine if there is a water pool that is cooler than us

		// Assuming no water pool, see if our temperature is above that of the air
		// if (data.bodyState.heatContainer.GetTemperatureRelativeToAir() > 1)
		// {
		// 	//Debug.Log("Airing");
		// 	return ActionRunState.Continue;
		// }

		if (data.bodyState.heatContainer.GetAirTemperature() / data.bodyState.cooling.GetMaxHeat() > 0.3f)
		{
			//Debug.Log("Airing");
			return ActionRunState.Continue;
		}

		//Debug.Log("Too hot");
		//There are no water pools and the air is too hot
		return ActionRunState.Stop;


		// bool seePlayer = false;
		// if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		// {
		// 	Vector3 direction1 = (Colliders[0].transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
		// 	RaycastHit hit1;
		// 	if (Physics.Raycast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
		// 	{
		// 		seePlayer = hit1.transform.GetComponent<PlayerController>() != null;
		// 	}
		// }

		// if (seePlayer)
		// {
		// 	return ActionRunState.Stop;
		// }

		//return ActionRunState.Continue;
	}

	public override void End(IMonoAgent agent, CommonData data) { }
	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}