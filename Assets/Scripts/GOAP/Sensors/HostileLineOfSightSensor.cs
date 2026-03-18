using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using UnityEngine;

public class HostileLineOfSightSensor : LocalWorldSensorBase, IInjectable
{
	private const float SenseIntervalSeconds = 0.1f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;

	private AttackConfigSO AttackConfig;
	private readonly System.Collections.Generic.Dictionary<int, AgentRuntimeState> AgentStates = new System.Collections.Generic.Dictionary<int, AgentRuntimeState>();

	private sealed class AgentRuntimeState
	{
		public SenseValue CachedValue;
		public bool HasCachedValue;
		public float NextSenseTime;
		public Vector3 LastAgentPosition;
		public Transform LastTargetTransform;
		public Vector3 LastTargetPosition;
	}

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override SenseValue Sense(IMonoAgent agent, IComponentReference references)
	{
		var perception = SharedAgentPerception.GetSnapshot(agent, references, AttackConfig);
		var runtimeState = GetRuntimeState(agent);
		if (perception.BodyState == null)
		{
			return new SenseValue(1);
		}

		if (CanReuseCachedResult(agent.transform.position, runtimeState, perception))
		{
			return runtimeState.CachedValue;
		}

		bool enemyHasLOS = !perception.IsTargetObstructed;
		Transform targetTransform = perception.TargetTransform;
		SenseValue value = new SenseValue(enemyHasLOS ? 1 : 0);
		runtimeState.CachedValue = value;
		runtimeState.HasCachedValue = true;
		runtimeState.LastAgentPosition = agent.transform.position;
		runtimeState.LastTargetTransform = targetTransform;
		if (targetTransform != null)
		{
			runtimeState.LastTargetPosition = targetTransform.position;
		}
		runtimeState.NextSenseTime = Time.time + SenseIntervalSeconds + (Mathf.Abs(agent.transform.GetInstanceID()) % 5) * 0.01f;
		return value;
		//return new SenseValue(Mathf.CeilToInt(references.GetCachedComponent<NPCBrain>().bodyState.HeatContainer_getCurrentHeat()));
	}

	private AgentRuntimeState GetRuntimeState(IMonoAgent agent)
	{
		int key = agent.transform.GetInstanceID();
		if (!AgentStates.TryGetValue(key, out AgentRuntimeState state))
		{
			state = new AgentRuntimeState();
			AgentStates.Add(key, state);
		}

		return state;
	}

	private bool CanReuseCachedResult(Vector3 agentPosition, AgentRuntimeState runtimeState, SharedAgentPerception.Snapshot perception)
	{
		if (!runtimeState.HasCachedValue || Time.time >= runtimeState.NextSenseTime)
		{
			return false;
		}

		if ((agentPosition - runtimeState.LastAgentPosition).sqrMagnitude > AgentMoveThresholdSqr)
		{
			return false;
		}

		if (runtimeState.LastTargetTransform != perception.TargetTransform)
		{
			return false;
		}

		if (perception.TargetTransform != null)
		{
			if ((perception.TargetPosition - runtimeState.LastTargetPosition).sqrMagnitude > TargetMoveThresholdSqr)
			{
				return false;
			}
		}

		return true;
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
