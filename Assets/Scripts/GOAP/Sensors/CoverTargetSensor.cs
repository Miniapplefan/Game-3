using System.Collections.Generic;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class CoverTargetSensor : LocalTargetSensorBase, IInjectable
{
	private const float SenseIntervalSeconds = 0.2f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;
	private static readonly float[] CoverCandidateDistances = { 2f, 4f, 10f };

	private readonly Dictionary<int, AgentRuntimeState> AgentStates = new Dictionary<int, AgentRuntimeState>();
	private readonly List<CoverCandidate> CoverCandidates = new List<CoverCandidate>(20);
	private readonly NavMeshPath SharedNavMeshPath = new NavMeshPath();
	private AttackConfigSO AttackConfig;

	public float navMeshSampleRadius = 2.5f;
	public float navMeshEdgeClearance = 0.2f;
	public float maxLocalCoverDistance = 11f;
	public float coverCommitDuration = 0.75f;
	public float coverCandidateDedupDistance = 0.5f;
	public float coverHeadHeightFallback = 1.5f;
	public float coverDistancePenaltyWeight = 1f;
	public float coverAwayFromTargetWeight = 1.5f;
	public float coverIncumbentBonus = 4f;
	public bool logCoverSelectionDebug = false;
	public bool logCoverCandidateDebug = false;

	private enum CoverCandidateSource
	{
		Away,
		Left,
		Right,
		BackLeft,
		BackRight,
		Incumbent,
	}

	private sealed class AgentRuntimeState
	{
		public Vector3 CachedPosition;
		public bool HasCachedPosition;
		public float NextSenseTime;
		public Vector3 LastAgentPosition;
		public Transform LastTargetTransform;
		public Vector3 LastTargetPosition;
		public bool HasCommittedCoverPosition;
		public Vector3 CommittedCoverPosition;
		public float CommittedCoverUntilTime;
		public Transform CommittedTargetTransform;
		public Vector3 CommittedTargetPosition;
	}

	private readonly struct CoverSenseContext
	{
		public CoverSenseContext(
			IMonoAgent agent,
			SharedAgentPerception.Snapshot perception,
			AgentRuntimeState runtimeState,
			BodyState bodyState,
			NavMeshAgent navMeshAgent,
			Vector3 agentPosition,
			Transform targetTransform,
			Vector3 targetPosition,
			Vector3 targetAimPosition)
		{
			Agent = agent;
			Perception = perception;
			RuntimeState = runtimeState;
			BodyState = bodyState;
			NavMeshAgent = navMeshAgent;
			AgentPosition = agentPosition;
			TargetTransform = targetTransform;
			TargetPosition = targetPosition;
			TargetAimPosition = targetAimPosition;
		}

		public IMonoAgent Agent { get; }
		public SharedAgentPerception.Snapshot Perception { get; }
		public AgentRuntimeState RuntimeState { get; }
		public BodyState BodyState { get; }
		public NavMeshAgent NavMeshAgent { get; }
		public Vector3 AgentPosition { get; }
		public Transform TargetTransform { get; }
		public Vector3 TargetPosition { get; }
		public Vector3 TargetAimPosition { get; }
		public bool HasTarget => Perception.HasTarget;
	}

	private struct CoverCandidate
	{
		public Vector3 Position;
		public CoverCandidateSource Source;
		public float DistanceToAgent;
		public float AwayFromTargetScore;
		public bool IsIncumbent;
	}

	private struct CoverDebugStats
	{
		public int Generated;
		public int Accepted;
		public int RejectedNavMesh;
		public int RejectedTooFar;
		public int RejectedDuplicate;
		public int RejectedExposed;
		public int SkippedDistance;
		public int InvalidDirection;
		public int CommittedRejected;

		public void Reset()
		{
			Generated = 0;
			Accepted = 0;
			RejectedNavMesh = 0;
			RejectedTooFar = 0;
			RejectedDuplicate = 0;
			RejectedExposed = 0;
			SkippedDistance = 0;
			InvalidDirection = 0;
			CommittedRejected = 0;
		}
	}

	private CoverDebugStats DebugStats;

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		CoverSenseContext context = BuildSenseContext(agent, references);
		if (context.BodyState == null || context.NavMeshAgent == null || !context.HasTarget)
		{
			ClearCommittedCover(context.RuntimeState);
			LogCoverSelection(context, $"fallback current position: bodyState={(context.BodyState != null)}, navAgent={(context.NavMeshAgent != null)}, hasTarget={context.HasTarget}");
			return CachePosition(context, context.AgentPosition, null);
		}

		if (context.BodyState.legs.getMoveSpeed() <= 0)
		{
			ClearCommittedCover(context.RuntimeState);
			LogCoverSelection(context, "fallback current position: legs cannot move");
			return CachePosition(context, context.AgentPosition, context.TargetTransform);
		}

		if (CanReuseCachedResult(context.AgentPosition, context.RuntimeState, context.Perception))
		{
			LogCoverSelection(context, $"reusing cached cover target {context.RuntimeState.CachedPosition} distance={Vector3.Distance(context.AgentPosition, context.RuntimeState.CachedPosition):F2}");
			return new PositionTarget(context.RuntimeState.CachedPosition);
		}

		if (TrySelectCoverPosition(context, out Vector3 coverPosition))
		{
			return CachePosition(context, coverPosition, context.TargetTransform);
		}

		ClearCommittedCover(context.RuntimeState);
		LogCoverSelection(context, $"no valid local cover candidates. {FormatDebugStats()}");
		return CachePosition(context, context.AgentPosition, context.TargetTransform);
	}

	private CoverSenseContext BuildSenseContext(IMonoAgent agent, IComponentReference references)
	{
		SharedAgentPerception.Snapshot perception = SharedAgentPerception.GetSnapshot(agent, references, AttackConfig);
		AgentRuntimeState runtimeState = GetRuntimeState(agent);
		BodyState bodyState = perception.BodyState;
		NavMeshAgent agentNavMeshAgent = perception.NavMeshAgent;
		Vector3 agentPosition = agent.transform.position;
		Vector3 targetAimPosition = perception.TargetHeadPosition;

		if (!perception.HasTarget)
		{
			targetAimPosition = perception.TargetPosition;
		}

		return new CoverSenseContext(
			agent,
			perception,
			runtimeState,
			bodyState,
			agentNavMeshAgent,
			agentPosition,
			perception.TargetTransform,
			perception.TargetPosition,
			targetAimPosition);
	}

	private bool TrySelectCoverPosition(CoverSenseContext context, out Vector3 coverPosition)
	{
		coverPosition = context.AgentPosition;
		CoverCandidates.Clear();
		DebugStats.Reset();

		TryAddCommittedCoverCandidate(context);
		CollectLocalCoverCandidates(context);

		if (CoverCandidates.Count == 0)
		{
			return false;
		}

		float bestScore = float.NegativeInfinity;
		int bestCandidateIndex = -1;
		for (int i = 0; i < CoverCandidates.Count; i++)
		{
			float score = ScoreCoverCandidate(CoverCandidates[i]);
			if (score > bestScore)
			{
				bestScore = score;
				bestCandidateIndex = i;
			}
		}

		if (bestCandidateIndex < 0)
		{
			return false;
		}

		CoverCandidate selected = CoverCandidates[bestCandidateIndex];
		coverPosition = selected.Position;
		CommitCoverPosition(context.RuntimeState, context.TargetTransform, context.TargetPosition, selected.Position);
		LogCoverSelection(context, $"selected {selected.Source} cover at {coverPosition} score={bestScore:F2}, distance={selected.DistanceToAgent:F2}, away={selected.AwayFromTargetScore:F2}. {FormatDebugStats()}");
		return true;
	}

	private void CollectLocalCoverCandidates(CoverSenseContext context)
	{
		Vector3 toTarget = Flatten(context.TargetPosition - context.AgentPosition);
		if (toTarget.sqrMagnitude < 0.0001f)
		{
			toTarget = Flatten(context.Agent.transform.forward);
		}

		if (toTarget.sqrMagnitude < 0.0001f)
		{
			toTarget = Vector3.forward;
		}

		toTarget.Normalize();
		Vector3 away = -toTarget;
		Vector3 right = Vector3.Cross(Vector3.up, toTarget).normalized;
		Vector3 left = -right;
		Vector3 backLeft = (away + left).normalized;
		Vector3 backRight = (away + right).normalized;

		AddDirectionalCandidates(context, away, CoverCandidateSource.Away);
		AddDirectionalCandidates(context, left, CoverCandidateSource.Left);
		AddDirectionalCandidates(context, right, CoverCandidateSource.Right);
		AddDirectionalCandidates(context, backLeft, CoverCandidateSource.BackLeft);
		AddDirectionalCandidates(context, backRight, CoverCandidateSource.BackRight);
	}

	private void AddDirectionalCandidates(CoverSenseContext context, Vector3 direction, CoverCandidateSource source)
	{
		if (direction.sqrMagnitude < 0.0001f)
		{
			DebugStats.InvalidDirection++;
			return;
		}

		Vector3 normalizedDirection = direction.normalized;
		for (int i = 0; i < CoverCandidateDistances.Length; i++)
		{
			float distance = CoverCandidateDistances[i];
			if (distance > maxLocalCoverDistance)
			{
				DebugStats.SkippedDistance++;
				continue;
			}

			Vector3 rawPoint = context.AgentPosition + normalizedDirection * distance;
			TryAddCoverCandidate(context, rawPoint, source, false);
		}
	}

	private void TryAddCommittedCoverCandidate(CoverSenseContext context)
	{
		AgentRuntimeState runtimeState = context.RuntimeState;
		if (!runtimeState.HasCommittedCoverPosition || Time.time > runtimeState.CommittedCoverUntilTime)
		{
			if (runtimeState.HasCommittedCoverPosition)
			{
				DebugStats.CommittedRejected++;
				LogCoverCandidate(context, CoverCandidateSource.Incumbent, runtimeState.CommittedCoverPosition, "rejected expired committed cover");
			}

			ClearCommittedCover(runtimeState);
			return;
		}

		if (runtimeState.CommittedTargetTransform != context.TargetTransform)
		{
			DebugStats.CommittedRejected++;
			LogCoverCandidate(context, CoverCandidateSource.Incumbent, runtimeState.CommittedCoverPosition, "rejected committed cover target changed");
			ClearCommittedCover(runtimeState);
			return;
		}

		if ((context.TargetPosition - runtimeState.CommittedTargetPosition).sqrMagnitude > TargetMoveThresholdSqr)
		{
			DebugStats.CommittedRejected++;
			LogCoverCandidate(context, CoverCandidateSource.Incumbent, runtimeState.CommittedCoverPosition, "rejected committed cover target moved");
			ClearCommittedCover(runtimeState);
			return;
		}

		TryAddCoverCandidate(context, runtimeState.CommittedCoverPosition, CoverCandidateSource.Incumbent, true);
	}

	private bool TryAddCoverCandidate(CoverSenseContext context, Vector3 rawPoint, CoverCandidateSource source, bool isIncumbent)
	{
		DebugStats.Generated++;
		if (!TryResolveReachablePoint(context.NavMeshAgent, rawPoint, out Vector3 reachablePoint, out string reachabilityFailure))
		{
			DebugStats.RejectedNavMesh++;
			LogCoverCandidate(context, source, rawPoint, $"rejected {reachabilityFailure}");
			return false;
		}

		float distanceToAgent = Vector3.Distance(context.AgentPosition, reachablePoint);
		if (distanceToAgent > maxLocalCoverDistance)
		{
			DebugStats.RejectedTooFar++;
			LogCoverCandidate(context, source, reachablePoint, $"rejected too far distance={distanceToAgent:F2}");
			return false;
		}

		if (IsDuplicateCandidate(reachablePoint))
		{
			DebugStats.RejectedDuplicate++;
			LogCoverCandidate(context, source, reachablePoint, "rejected duplicate");
			return false;
		}

		if (!IsCandidateCovered(context, reachablePoint))
		{
			DebugStats.RejectedExposed++;
			LogCoverCandidate(context, source, reachablePoint, "rejected exposed/no obstruction");
			return false;
		}

		Vector3 moveDirection = Flatten(reachablePoint - context.AgentPosition);
		Vector3 awayDirection = Flatten(context.AgentPosition - context.TargetPosition);
		float awayScore = 0f;
		if (moveDirection.sqrMagnitude > 0.0001f && awayDirection.sqrMagnitude > 0.0001f)
		{
			awayScore = Vector3.Dot(moveDirection.normalized, awayDirection.normalized);
		}

		CoverCandidates.Add(new CoverCandidate
		{
			Position = reachablePoint,
			Source = source,
			DistanceToAgent = distanceToAgent,
			AwayFromTargetScore = awayScore,
			IsIncumbent = isIncumbent,
		});
		DebugStats.Accepted++;
		LogCoverCandidate(context, source, reachablePoint, $"accepted distance={distanceToAgent:F2}, away={awayScore:F2}, incumbent={isIncumbent}");
		return true;
	}

	private bool IsDuplicateCandidate(Vector3 position)
	{
		float dedupDistanceSqr = coverCandidateDedupDistance * coverCandidateDedupDistance;
		for (int i = 0; i < CoverCandidates.Count; i++)
		{
			if ((CoverCandidates[i].Position - position).sqrMagnitude < dedupDistanceSqr)
			{
				return true;
			}
		}

		return false;
	}

	private bool IsCandidateCovered(CoverSenseContext context, Vector3 candidatePosition)
	{
		Vector3 candidateHeadPosition = GetCandidateHeadPosition(context.BodyState, candidatePosition);
		Vector3 direction = candidateHeadPosition - context.TargetAimPosition;
		float distance = direction.magnitude;
		if (distance <= 0.0001f)
		{
			return false;
		}

		return Physics.SphereCast(
			context.TargetAimPosition,
			AttackConfig.LineOfSightSphereCastRadius,
			direction / distance,
			out _,
			distance,
			AttackConfig.ObstructionLayerMask);
	}

	private Vector3 GetCandidateHeadPosition(BodyState bodyState, Vector3 candidatePosition)
	{
		float eyeHeight = coverHeadHeightFallback;
		if (bodyState != null && bodyState.headCollider != null)
		{
			eyeHeight = Mathf.Max(0.5f, bodyState.headCollider.bounds.center.y - bodyState.transform.position.y);
		}

		return candidatePosition + Vector3.up * eyeHeight;
	}

	private float ScoreCoverCandidate(CoverCandidate candidate)
	{
		float score = 0f;
		score -= candidate.DistanceToAgent * coverDistancePenaltyWeight;
		score += candidate.AwayFromTargetScore * coverAwayFromTargetWeight;
		score += GetSourceScore(candidate.Source);
		if (candidate.IsIncumbent)
		{
			score += coverIncumbentBonus;
		}

		return score;
	}

	private float GetSourceScore(CoverCandidateSource source)
	{
		switch (source)
		{
			case CoverCandidateSource.Away:
				return 0.3f;
			case CoverCandidateSource.BackLeft:
			case CoverCandidateSource.BackRight:
				return 0.4f;
			case CoverCandidateSource.Incumbent:
				return 0.5f;
			default:
				return 0f;
		}
	}

	private bool TryResolveReachablePoint(NavMeshAgent agentNavMeshAgent, Vector3 rawPoint, out Vector3 reachablePoint, out string failureReason)
	{
		failureReason = string.Empty;
		reachablePoint = agentNavMeshAgent != null ? agentNavMeshAgent.transform.position : rawPoint;
		int areaMask = agentNavMeshAgent != null ? agentNavMeshAgent.areaMask : NavMesh.AllAreas;

		if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSampleRadius, areaMask))
		{
			failureReason = "navmesh sample failed";
			return false;
		}

		reachablePoint = hit.position;
		float edgeClearance = navMeshEdgeClearance;
		if (agentNavMeshAgent != null)
		{
			edgeClearance = Mathf.Max(edgeClearance, agentNavMeshAgent.radius);
		}

		if (edgeClearance > 0f
			&& NavMesh.FindClosestEdge(reachablePoint, out NavMeshHit edgeHit, areaMask)
			&& edgeHit.distance < edgeClearance)
		{
			failureReason = $"edge clearance failed distance={edgeHit.distance:F2}, required={edgeClearance:F2}";
			return false;
		}

		if (agentNavMeshAgent == null || !agentNavMeshAgent.enabled || !agentNavMeshAgent.isOnNavMesh)
		{
			return true;
		}

		if ((reachablePoint - agentNavMeshAgent.transform.position).sqrMagnitude <= AgentMoveThresholdSqr)
		{
			return true;
		}

		if (!NavMesh.CalculatePath(agentNavMeshAgent.transform.position, reachablePoint, areaMask, SharedNavMeshPath))
		{
			failureReason = "path calculation failed";
			return false;
		}

		if (SharedNavMeshPath.status != NavMeshPathStatus.PathComplete)
		{
			failureReason = $"path incomplete status={SharedNavMeshPath.status}";
			return false;
		}

		return true;
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
		if (!runtimeState.HasCachedPosition || Time.time >= runtimeState.NextSenseTime)
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

		if (perception.TargetTransform != null
			&& (perception.TargetPosition - runtimeState.LastTargetPosition).sqrMagnitude > TargetMoveThresholdSqr)
		{
			return false;
		}

		return true;
	}

	private PositionTarget CachePosition(CoverSenseContext context, Vector3 position, Transform targetTransform)
	{
		AgentRuntimeState runtimeState = context.RuntimeState;
		runtimeState.CachedPosition = position;
		runtimeState.HasCachedPosition = true;
		runtimeState.LastAgentPosition = context.AgentPosition;
		runtimeState.LastTargetTransform = targetTransform;
		if (targetTransform != null)
		{
			runtimeState.LastTargetPosition = targetTransform.position;
		}

		runtimeState.NextSenseTime = Time.time + SenseIntervalSeconds + (Mathf.Abs(context.Agent.transform.GetInstanceID()) % 5) * 0.01f;
		return new PositionTarget(position);
	}

	private void CommitCoverPosition(AgentRuntimeState runtimeState, Transform targetTransform, Vector3 targetPosition, Vector3 coverPosition)
	{
		runtimeState.HasCommittedCoverPosition = true;
		runtimeState.CommittedCoverPosition = coverPosition;
		runtimeState.CommittedCoverUntilTime = Time.time + coverCommitDuration;
		runtimeState.CommittedTargetTransform = targetTransform;
		runtimeState.CommittedTargetPosition = targetPosition;
	}

	private void ClearCommittedCover(AgentRuntimeState runtimeState)
	{
		runtimeState.HasCommittedCoverPosition = false;
		runtimeState.CommittedCoverPosition = Vector3.zero;
		runtimeState.CommittedCoverUntilTime = 0f;
		runtimeState.CommittedTargetTransform = null;
		runtimeState.CommittedTargetPosition = Vector3.zero;
	}

	private Vector3 Flatten(Vector3 vector)
	{
		vector.y = 0f;
		return vector;
	}

	private void LogCoverSelection(CoverSenseContext context, string message)
	{
		if (!logCoverSelectionDebug)
		{
			return;
		}

		Debug.Log($"CoverTargetSensor: {message} for '{context.Agent.transform.name}'.");
	}

	private void LogCoverCandidate(CoverSenseContext context, CoverCandidateSource source, Vector3 position, string message)
	{
		if (!logCoverCandidateDebug)
		{
			return;
		}

		Debug.Log($"CoverTargetSensor: candidate {source} at {position} {message} for '{context.Agent.transform.name}'.");
	}

	private string FormatDebugStats()
	{
		return $"generated={DebugStats.Generated}, accepted={DebugStats.Accepted}, navmesh={DebugStats.RejectedNavMesh}, tooFar={DebugStats.RejectedTooFar}, duplicate={DebugStats.RejectedDuplicate}, exposed={DebugStats.RejectedExposed}, skippedDistance={DebugStats.SkippedDistance}, invalidDirection={DebugStats.InvalidDirection}, committedRejected={DebugStats.CommittedRejected}";
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
