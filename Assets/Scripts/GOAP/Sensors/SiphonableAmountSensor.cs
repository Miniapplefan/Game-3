using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;

public class SiphonableAmountSensor : LocalWorldSensorBase, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] TargetCollider = new Collider[1];
	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override SenseValue Sense(IMonoAgent agent, IComponentReference references)
	{
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, TargetCollider, AttackConfig.SiphonableLayerMask) > 0)
		{
			return new SenseValue(Mathf.CeilToInt(TargetCollider[0].gameObject.GetComponent<SiphonTarget>().dollarsLeft));
		}
		else
		{
			return new SenseValue(1);
		}
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}