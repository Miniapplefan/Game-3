using System.Collections.Generic;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;
using UnityEngine.AI;

public static class SharedAgentPerception
{
	private const float RefreshIntervalSeconds = 0.1f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;

	private static readonly Dictionary<int, AgentPerceptionState> States = new Dictionary<int, AgentPerceptionState>();

	public readonly struct Snapshot
	{
		public Snapshot(
			BodyState bodyState,
			NavMeshAgent navMeshAgent,
			Vector3 agentPosition,
			Transform targetTransform,
			BodyState targetBodyState,
			Vector3 targetPosition,
			Vector3 targetHeadPosition,
			bool hasTarget,
			bool canSeeTarget,
			bool isTargetObstructed)
		{
			BodyState = bodyState;
			NavMeshAgent = navMeshAgent;
			AgentPosition = agentPosition;
			TargetTransform = targetTransform;
			TargetBodyState = targetBodyState;
			TargetPosition = targetPosition;
			TargetHeadPosition = targetHeadPosition;
			HasTarget = hasTarget;
			CanSeeTarget = canSeeTarget;
			IsTargetObstructed = isTargetObstructed;
		}

		public BodyState BodyState { get; }
		public NavMeshAgent NavMeshAgent { get; }
		public Vector3 AgentPosition { get; }
		public Transform TargetTransform { get; }
		public BodyState TargetBodyState { get; }
		public Vector3 TargetPosition { get; }
		public Vector3 TargetHeadPosition { get; }
		public bool HasTarget { get; }
		public bool CanSeeTarget { get; }
		public bool IsTargetObstructed { get; }
	}

	private sealed class AgentPerceptionState
	{
		public readonly Collider[] TargetColliders = new Collider[1];
		public BodyState BodyState;
		public NavMeshAgent NavMeshAgent;
		public Vector3 LastAgentPosition;
		public Transform TargetTransform;
		public BodyState TargetBodyState;
		public Vector3 TargetPosition;
		public Vector3 TargetHeadPosition;
		public Vector3 LastTargetPosition;
		public float NextRefreshTime;
		public bool HasTarget;
		public bool CanSeeTarget;
		public bool IsTargetObstructed;
		public bool IsInitialized;
	}

	public static Snapshot GetSnapshot(IMonoAgent agent, IComponentReference references, AttackConfigSO attackConfig)
	{
		int key = agent.transform.GetInstanceID();
		if (!States.TryGetValue(key, out AgentPerceptionState state))
		{
			state = new AgentPerceptionState();
			States.Add(key, state);
		}

		if (state.BodyState == null)
		{
			state.BodyState = references.GetCachedComponentInChildren<BodyState>();
		}

		if (state.NavMeshAgent == null)
		{
			state.NavMeshAgent = agent.GetComponent<NavMeshAgent>();
		}

		Vector3 agentPosition = agent.transform.position;
		if (NeedsRefresh(state, agentPosition))
		{
			Refresh(state, agentPosition, attackConfig, key);
		}

		return new Snapshot(
			state.BodyState,
			state.NavMeshAgent,
			agentPosition,
			state.TargetTransform,
			state.TargetBodyState,
			state.TargetPosition,
			state.TargetHeadPosition,
			state.HasTarget,
			state.CanSeeTarget,
			state.IsTargetObstructed);
	}

	public static bool HasLineOfSight(Vector3 start, Vector3 end, AttackConfigSO attackConfig)
	{
		if (Physics.SphereCast(start, attackConfig.LineOfSightSphereCastRadius, (end - start).normalized, out RaycastHit hit, Mathf.Infinity, attackConfig.AttackableLayerMask | attackConfig.ObstructionLayerMask))
		{
			return hit.transform.GetComponent<PlayerController>() != null;
		}

		return false;
	}

	private static bool NeedsRefresh(AgentPerceptionState state, Vector3 agentPosition)
	{
		if (!state.IsInitialized || Time.time >= state.NextRefreshTime)
		{
			return true;
		}

		if ((agentPosition - state.LastAgentPosition).sqrMagnitude > AgentMoveThresholdSqr)
		{
			return true;
		}

		if (state.TargetTransform != null && (state.TargetTransform.position - state.LastTargetPosition).sqrMagnitude > TargetMoveThresholdSqr)
		{
			return true;
		}

		return false;
	}

	private static void Refresh(AgentPerceptionState state, Vector3 agentPosition, AttackConfigSO attackConfig, int agentId)
	{
		state.TargetColliders[0] = null;
		state.LastAgentPosition = agentPosition;
		state.IsInitialized = true;

		if (Physics.OverlapSphereNonAlloc(agentPosition, attackConfig.SensorRadius, state.TargetColliders, attackConfig.AttackableLayerMask) > 0 && state.TargetColliders[0] != null)
		{
			state.TargetTransform = state.TargetColliders[0].transform;
			state.TargetBodyState = state.TargetTransform.GetComponentInParent<BodyState>();
			state.TargetPosition = state.TargetTransform.position;
			state.LastTargetPosition = state.TargetPosition;
			state.TargetHeadPosition = state.TargetBodyState != null && state.TargetBodyState.headCollider != null
				? state.TargetBodyState.headCollider.transform.position
				: state.TargetPosition;
			state.HasTarget = true;

			if (state.BodyState != null && state.BodyState.headCollider != null)
			{
				if (Physics.SphereCast(state.BodyState.headCollider.transform.position, attackConfig.LineOfSightSphereCastRadius, (state.TargetHeadPosition - state.BodyState.headCollider.transform.position).normalized, out RaycastHit hit, Mathf.Infinity, attackConfig.AttackableLayerMask | attackConfig.ObstructionLayerMask))
				{
					bool hitPlayer = hit.transform.GetComponent<PlayerController>() != null;
					state.CanSeeTarget = hitPlayer;
					state.IsTargetObstructed = !hitPlayer;
				}
				else
				{
					state.CanSeeTarget = false;
					state.IsTargetObstructed = false;
				}
			}
			else
			{
				state.CanSeeTarget = false;
				state.IsTargetObstructed = false;
			}
		}
		else
		{
			state.TargetTransform = null;
			state.TargetBodyState = null;
			state.TargetPosition = agentPosition;
			state.TargetHeadPosition = agentPosition;
			state.LastTargetPosition = agentPosition;
			state.HasTarget = false;
			state.CanSeeTarget = false;
			state.IsTargetObstructed = false;
		}

		state.NextRefreshTime = Time.time + RefreshIntervalSeconds + (Mathf.Abs(agentId) % 5) * 0.01f;
	}
}
