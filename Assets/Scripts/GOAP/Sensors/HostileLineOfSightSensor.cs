using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;

public class HostileLineOfSightSensor : LocalWorldSensorBase, IInjectable
{

	private Collider[] Colliders = new Collider[1];
	private AttackConfigSO AttackConfig;

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override SenseValue Sense(IMonoAgent agent, IComponentReference references)
	{
		//agent.GetComponentInChildren<BodyState>().positionTracker.transform.position = agent.GetComponentInChildren<BodyState>().rightArm.transform.position;
		//agent.GetComponentInChildren<BodyState>().positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
		bool enemyHasLOS = true;
		//		Debug.Log(AttackConfig == null ? "AttackConfig Is null" : "AttackConfig Is OK");
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			//Player is in range, check if we can see them
			RaycastHit hit1;
			Vector3 direction1 = (Colliders[0].transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
			if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
			{
				//Debug.Log(hit1.collider.gameObject.name);
				if (hit1.transform.GetComponent<PlayerController>() == null)
				{
					enemyHasLOS = false;
					//agent.GetComponentInChildren<BodyState>().positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.red;
				}
				else
				{
					//Debug.Log("No LOS");
					enemyHasLOS = true;
				}
			}
			else
			{
				//Debug.Log("No LOS");
				enemyHasLOS = true;
			}
		}
		else
		{
			//Debug.Log("No LOS");
			enemyHasLOS = true;
		}
		if (enemyHasLOS)
		{
			// Debug.Log("LOS");
			//agent.GetComponentInChildren<BodyState>().positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.red;

		}
		else
		{
			//			Debug.Log("No LOS");
			//agent.GetComponentInChildren<BodyState>().positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
		}
		return new SenseValue(enemyHasLOS == true ? 1 : 0);
		//return new SenseValue(Mathf.CeilToInt(references.GetCachedComponent<NPCBrain>().bodyState.HeatContainer_getCurrentHeat()));
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}