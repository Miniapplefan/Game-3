using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Enums;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;

public class TakeCoverAction : ActionBase<CommonData>, IInjectable
{
	public override void Created() { }

	public override void Start(IMonoAgent agent, CommonData data)
	{
		data.Timer = 0.2f;
	}

	public override ActionRunState Perform(IMonoAgent agent, CommonData data, ActionContext context)
	{
		data.Timer -= context.DeltaTime;

		if (data.bodyState.dangerLevel < 0.1f) return ActionRunState.Stop;

		return data.Timer > 0f ? ActionRunState.Continue : ActionRunState.Stop;
	}

	public override void End(IMonoAgent agent, CommonData data) { }
	public void Inject(DependencyInjector injector) { }
}
