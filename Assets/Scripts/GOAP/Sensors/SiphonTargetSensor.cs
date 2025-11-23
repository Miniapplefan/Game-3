using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;

public class SiphonTargetSensor : LocalTargetSensorBase, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] TargetCollider = new Collider[1];

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		Vector3 position = GetSiphonablePosition(agent);

		return new PositionTarget(position);
	}

	private Vector3 GetSiphonablePosition(IMonoAgent agent)
	{
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, TargetCollider, AttackConfig.SiphonableLayerMask) > 0)
		{
			agent.GetComponentInChildren<BodyState>().siphonTarget = TargetCollider[0].GetComponent<SiphonTarget>();
			return TargetCollider[0].transform.position;
		}
		else
		{
			Debug.Log("Failed to find siphontarget");
			return agent.transform.position;
		}
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}