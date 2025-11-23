using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Enums;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;

public class DeploySiphonAction : ActionBase<AttackData>, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] Colliders = new Collider[1];

	public override void Start(IMonoAgent agent, AttackData data)
	{
		data.Timer = AttackConfig.SiphonDelay;
	}
	public override void Created()
	{
	}

	public override ActionRunState Perform(IMonoAgent agent, AttackData data, ActionContext context)
	{
		data.Timer -= context.DeltaTime;
		if (agent.GetComponentInChildren<BodyState>().siphon.extended)
		{
			data.AIController.pressingSiphon = false;
		}
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.SiphonableLayerMask) > 0)
		{
			data.AIController.SetAimTarget(Colliders[0].transform.position);
			data.bodyState.siphonTarget = Colliders[0].gameObject.GetComponent<SiphonTarget>();
		}

		bool shouldDeploy = Vector3.Distance(agent.transform.position, Colliders[0].transform.position) < 2f;
		if (shouldDeploy && !agent.GetComponentInChildren<BodyState>().siphon.extended)
		{
			//Debug.Log(Vector3.Distance(agent.transform.position, Colliders[0].transform.position));
			data.AIController.pressingSiphon = true;
		}
		else
		{
			return ActionRunState.Stop;
		}
		return data.Timer > 0 ? ActionRunState.Continue : ActionRunState.Stop;
	}

	public override void End(IMonoAgent agent, AttackData data)
	{
		if (data.bodyState.Siphon_isExtended())
		{
			data.AIController.pressingSiphon = false;
		}
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}