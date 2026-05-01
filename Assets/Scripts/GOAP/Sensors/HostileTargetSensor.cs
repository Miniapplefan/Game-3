using System.Collections.Generic;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class HostileTargetSensor : LocalTargetSensorBase, IInjectable
{
	// Sense/cache throttling
	private const float SenseIntervalSeconds = 0.2f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;

	// Candidate sampling budgets
	private const int MaxCirclePointSamples = 12;
	private const int MaxFallbackPointSamples = 8;
	private const int MaxStrafePointSamples = 12;
	private const int RadialSectorCount = 9;
	private const int TacticalSectorBandSamples = 6;
	private const int TacticalLocalAgentSamples = 6;
	private const int TacticalPlayerAnnulusSamples = 6;
	private const int TacticalAllowedSectorOffset = 1;
	private const int CirclePointsPerSense = 4;
	private const int FallbackPointsPerSense = 4;
	private const int StrafePointsPerSense = 4;
	private const int MaxRandomCircleAttempts = 6;

	// Player annulus candidate recipe
	private static readonly float[] TacticalAnnulusRadiusFractions = { 0.2f, 0.5f, 0.8f, 0.35f, 0.65f, 0.9f };
	private static readonly int[] TacticalAnnulusSectorOffsets = { 0, 0, 0, -1, 1, 0 };
	private static readonly float[] TacticalAnnulusSectorFractions = { -0.35f, 0.35f, 0f, 0f, 0f, 0.18f };

	private AttackConfigSO AttackConfig;

	// Circle and ally spacing fallback
	public float circleRadius = 5f;
	public int numberOfPoints = 36;
	public float minAllySeparationAngle = 20f;
	public float allySeparationWeight = 12f;
	public float distancePenaltyWeight = 0.5f;
	public float allySearchRadiusMultiplier = 1.25f;

	// NavMesh validation
	public float navMeshSampleRadius = 2.5f;
	public float navMeshEdgeClearance = 0.6f;

	// Agent-local fallback movement
	public float agentFallbackRadius = 6f;
	public int agentFallbackPoints = 16;
	public float agentFallbackPlayerWeight = 1f;

	// Radial claim spacing
	public float radialMinDistanceMultiplier = 0.5f;
	public float radialMaxDistanceMultiplier = 0.75f;
	public float radialDistanceStep = 1.5f;
	public float radialClaimDuration = 0.5f;
	public float radialDestinationReachedDistance = 0.75f;
	public float radialClaimReuseTargetMoveThreshold = 1f;
	public float radialClaimCleanupTargetMoveThreshold = 2f;
	public float radialClaimRelevanceDistanceMultiplier = 1.5f;
	public float radialSectorSwitchCooldown = 0.75f;
	public int radialSectorFailureThreshold = 3;
	public int radialClaimSectorExclusionRadius = 1;
	public float radialRecoveryMinDistanceMultiplier = 0.5f;
	public float radialRecoveryMaxDistanceMultiplier = 1f;

	// Tactical candidate generation
	public float tacticalClaimedPositionSeparation = 2f;
	public float tacticalLocalSampleInnerRadius = 2f;
	public float tacticalLocalSampleOuterRadius = 5f;
	public bool enablePlayerAnnulusCandidates = false;

	// Tactical candidate scoring
	public float tacticalDesiredSectorScore = 400f;
	public float tacticalNearbySectorScore = 220f;
	public float tacticalGapScoreWeight = 55f;
	public float tacticalGapCenterPenaltyWeight = 18f;
	public float tacticalSourcePriorityWeight = 12f;
	public float tacticalUnclaimedPositionBonus = 80f;
	public float tacticalClaimedSeparationBonusMaxDistance = 12f;
	public float tacticalClaimedSeparationBonusWeight = 10f;
	public float tacticalNearbyPositionPenaltyWeight = 0.15f;
	public float tacticalCurrentPositionStickinessBonus = 140f;
	public float tacticalSwitchScoreMargin = 80f;

	// Visible hold behavior
	public float visiblePositionDwellDuration = 1.5f;
	public int visibleHoldFailureThreshold = 3;

	// Tactical debug visualization
	public bool drawTacticalQueryGizmos = true;
	public bool spawnTacticalQueryDebugObjects = false;
	public bool logTacticalQuerySelections = true;
	public bool logFallbackSelections = true;
	public string tacticalDebugAgentName = "NPC_total new";
	public string tacticalDebugTargetName = "Body_total new";
	public float tacticalDebugCandidateMarkerScale = 0.35f;
	public float tacticalDebugRingMarkerScale = 0.12f;
	public int tacticalDebugRingMarkerCount = 24;

	// Runtime query state
	private Collider[] AllyColliders = new Collider[32];
	private List<Vector3> AllyPositions = new List<Vector3>(32);
	private List<Transform> AllyRoots = new List<Transform>(32);
	private readonly List<TacticalCandidate> TacticalCandidates = new List<TacticalCandidate>(TacticalSectorBandSamples + TacticalLocalAgentSamples + TacticalPlayerAnnulusSamples);
	private readonly List<TacticalCandidateDebugSnapshot> TacticalCandidateDebugSnapshots = new List<TacticalCandidateDebugSnapshot>(TacticalSectorBandSamples + TacticalLocalAgentSamples + TacticalPlayerAnnulusSamples);
	private readonly List<TacticalProbeDebugSnapshot> TacticalProbeDebugSnapshots = new List<TacticalProbeDebugSnapshot>(TacticalSectorBandSamples + TacticalLocalAgentSamples + TacticalPlayerAnnulusSamples);
	private readonly List<GameObject> TacticalDebugCandidateObjects = new List<GameObject>(TacticalSectorBandSamples + TacticalLocalAgentSamples + TacticalPlayerAnnulusSamples);
	private readonly List<GameObject> TacticalDebugProbeObjects = new List<GameObject>(TacticalSectorBandSamples + TacticalLocalAgentSamples + TacticalPlayerAnnulusSamples);
	private readonly List<GameObject> TacticalDebugInnerRingObjects = new List<GameObject>(24);
	private readonly List<GameObject> TacticalDebugOuterRingObjects = new List<GameObject>(24);
	private readonly List<int> ClaimCleanupAgentIds = new List<int>(16);
	private readonly Dictionary<int, AgentRuntimeState> AgentStates = new Dictionary<int, AgentRuntimeState>();
	private readonly NavMeshPath SharedNavMeshPath = new NavMeshPath();
	private Transform LastTacticalDebugAgent;
	private Transform LastTacticalDebugTarget;
	private Vector3 LastTacticalDebugTargetPosition;
	private int LastTacticalDebugDesiredSector = -1;
	private float LastTacticalDebugMinDistance;
	private float LastTacticalDebugMaxDistance;
	private Vector3 LastTacticalDebugBestPoint;
	private bool HasLastTacticalDebugBestPoint;
	private GameObject TacticalDebugRoot;
	private bool HasLoggedTacticalDebugCapture;
	private bool HasLastLoggedTacticalSelection;
	private Vector3 LastLoggedTacticalSelectionPoint;

	private sealed class AgentRuntimeState
	{
		public Vector3 CachedPosition;
		public bool HasCachedPosition;
		public float NextSenseTime;
		public Vector3 LastAgentPosition;
		public Transform LastTargetTransform;
		public Vector3 LastTargetPosition;
		public int CirclePointStartIndex;
		public int FallbackPointStartIndex;
		public int StrafePointStartIndex;
		public int LastClaimedSectorIndex = -1;
		public float NextAllowedSectorSwitchTime;
		public int ConsecutiveClaimedSectorFailures;
		public float VisibleHoldUntilTime;
		public int ConsecutiveVisibleHoldFailures;
		public bool HasVisibleHoldAnchor;
		public Vector3 VisibleHoldAnchorTargetPosition;
	}

	private sealed class AngleClaim
	{
		public int AgentId;
		public int TargetId;
		public int SectorIndex = -1;
		public float LastUpdatedTime;
		public float ExpiresAt;
		public Vector3 LastKnownAgentPosition;
		public Vector3 LastKnownTargetPosition;
		public Vector3 ClaimedPosition;
	}

	private struct TacticalCandidate
	{
		public Vector3 Position;
		public int SectorIndex;
		public int SectorDistanceFromDesired;
		public int GapScore;
		public int GapCenterOffset;
		public float DistanceToAgentSqr;
		public float MinClaimedDistanceSqr;
		public int SourcePriority;
		public bool IsIncumbent;
	}

	private struct TacticalCandidateDebugSnapshot
	{
		public Vector3 Position;
		public int SourcePriority;
		public bool IsSelected;
	}

	private enum TacticalProbeDebugResult
	{
		Accepted,
		NavMeshRejected,
		DistanceRejected,
		SectorRejected,
		OwnershipRejected,
		LineOfSightRejected,
		ClaimSeparationRejected,
		DedupedRejected,
	}

	private struct TacticalProbeDebugSnapshot
	{
		public Vector3 Position;
		public TacticalProbeDebugResult Result;
	}

	private sealed class TargetAngleClaimState
	{
		public int TargetId;
		public Transform TargetTransform;
		public readonly Dictionary<int, AngleClaim> ClaimsByAgent = new Dictionary<int, AngleClaim>();
	}

	private static class SharedAngleClaims
	{
		private static readonly Dictionary<int, TargetAngleClaimState> ClaimsByTarget = new Dictionary<int, TargetAngleClaimState>();

		public static bool TryGetTargetState(Transform targetTransform, out TargetAngleClaimState state)
		{
			state = null;
			if (targetTransform == null)
			{
				return false;
			}

			return ClaimsByTarget.TryGetValue(targetTransform.GetInstanceID(), out state);
		}

		public static TargetAngleClaimState GetOrCreateTargetState(Transform targetTransform)
		{
			if (targetTransform == null)
			{
				return null;
			}

			int targetId = targetTransform.GetInstanceID();
			if (!ClaimsByTarget.TryGetValue(targetId, out TargetAngleClaimState state))
			{
				state = new TargetAngleClaimState
				{
					TargetId = targetId,
					TargetTransform = targetTransform,
				};
				ClaimsByTarget.Add(targetId, state);
			}
			else if (state.TargetTransform == null)
			{
				state.TargetTransform = targetTransform;
			}

			return state;
		}

		public static bool TryGetClaim(Transform targetTransform, int agentId, out AngleClaim claim)
		{
			claim = null;
			return TryGetTargetState(targetTransform, out TargetAngleClaimState state)
				&& state.ClaimsByAgent.TryGetValue(agentId, out claim);
		}

		public static AngleClaim GetOrCreateClaim(Transform targetTransform, int agentId)
		{
			TargetAngleClaimState state = GetOrCreateTargetState(targetTransform);
			if (state == null)
			{
				return null;
			}

			if (!state.ClaimsByAgent.TryGetValue(agentId, out AngleClaim claim))
			{
				claim = new AngleClaim
				{
					AgentId = agentId,
					TargetId = state.TargetId,
				};
				state.ClaimsByAgent.Add(agentId, claim);
			}

			return claim;
		}

		public static bool RemoveClaim(Transform targetTransform, int agentId)
		{
			return TryGetTargetState(targetTransform, out TargetAngleClaimState state)
				&& state.ClaimsByAgent.Remove(agentId);
		}

		public static void ClearTargetClaims(Transform targetTransform)
		{
			if (targetTransform == null)
			{
				return;
			}

			ClaimsByTarget.Remove(targetTransform.GetInstanceID());
		}
	}

	private readonly struct SenseContext
	{
		public SenseContext(
			IMonoAgent agent,
			SharedAgentPerception.Snapshot perception,
			AgentRuntimeState runtimeState,
			BodyState bodyState,
			Transform targetTransform,
			Vector3 agentPosition,
			Vector3 targetPosition,
			Vector3 targetAimPosition,
			float distanceToTarget)
		{
			Agent = agent;
			Perception = perception;
			RuntimeState = runtimeState;
			BodyState = bodyState;
			TargetTransform = targetTransform;
			AgentPosition = agentPosition;
			TargetPosition = targetPosition;
			TargetAimPosition = targetAimPosition;
			DistanceToTarget = distanceToTarget;
		}

		public IMonoAgent Agent { get; }
		public SharedAgentPerception.Snapshot Perception { get; }
		public AgentRuntimeState RuntimeState { get; }
		public BodyState BodyState { get; }
		public Transform TargetTransform { get; }
		public Vector3 AgentPosition { get; }
		public Vector3 TargetPosition { get; }
		public Vector3 TargetAimPosition { get; }
		public float WeaponRange => BodyState != null && BodyState.desiredGunToUse != null
			? BodyState.desiredGunToUse.gunData.shootConfig.maxRange
			: 10f;
		public float DistanceToTarget { get; }
		public bool HasTarget => Perception.HasTarget;
		public bool CanSeeTarget => Perception.CanSeeTarget;
	}

	public override void Created()
	{
		if (spawnTacticalQueryDebugObjects && Application.isPlaying)
		{
			EnsureRuntimeDebugRoot();
			Debug.Log("HostileTargetSensor: TacticalQueryDebug root created.");
		}
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		var context = BuildSenseContext(agent, references);

		if (TryResolveMissingBodyState(context, out Vector3 position))
		{
			return new PositionTarget(position);
		}

		if (TryResolveImmobilePosition(context, out position))
		{
			return CachePosition(context, position, null);
		}

		if (TryReuseCachedPosition(context, out position))
		{
			return new PositionTarget(position);
		}

		if (context.HasTarget)
		{
			if (TryHoldVisibleAttackPosition(context, out position))
				return CachePosition(context, position, context.TargetTransform);

			if (TryFindVisibleAttackPosition(context, out position))
				return CachePosition(context, position, context.TargetTransform);

			if (TryFindRecoveryAttackPosition(context, out position))
				return CachePosition(context, position, context.TargetTransform);

			return CachePosition(context, context.AgentPosition, context.TargetTransform);
		}

		if (TryFindLostTargetAdvancePosition(context, out position, out Transform lostTargetTransform))
		{
			return CachePosition(context, position, lostTargetTransform);
		}

		TryFindRandomIdlePosition(context, out position);
		return CachePosition(context, position, null);
	}

	private SenseContext BuildSenseContext(IMonoAgent agent, IComponentReference references)
	{
		var perception = SharedAgentPerception.GetSnapshot(agent, references, AttackConfig);
		var runtimeState = GetRuntimeState(agent);
		var bodyState = perception.BodyState;
		Vector3 agentPosition = agent.transform.position;
		Vector3 targetPosition = perception.TargetPosition;

		return new SenseContext(
			agent,
			perception,
			runtimeState,
			bodyState,
			perception.TargetTransform,
			agentPosition,
			targetPosition,
			perception.TargetHeadPosition,
			Vector3.Distance(agentPosition, targetPosition));
	}

	// No BodyState means the sensor cannot reason about combat movement.
	private bool TryResolveMissingBodyState(SenseContext context, out Vector3 position)
	{
		position = context.AgentPosition;
		return context.BodyState == null;
	}

	// Keep immobilized agents from requesting tactical movement.
	private bool TryResolveImmobilePosition(SenseContext context, out Vector3 position)
	{
		position = context.AgentPosition;
		return context.BodyState.legs.getMoveSpeed() <= 0;
	}

	// Preserve the existing fast path to avoid expensive tactical queries every sense tick.
	private bool TryReuseCachedPosition(SenseContext context, out Vector3 position)
	{
		position = context.RuntimeState.CachedPosition;
		return CanReuseCachedResult(context.AgentPosition, context.RuntimeState, context.Perception);
	}

	// Preserve current behavior: if the agent can see the target in weapon range, hold position.
	private bool TryHoldVisibleAttackPosition(SenseContext context, out Vector3 position)
	{
		position = context.AgentPosition;
		if (!context.CanSeeTarget
			|| context.DistanceToTarget > context.WeaponRange
			|| ShouldBreakVisibleHoldFromTargetShift(context.RuntimeState, context.TargetPosition))
		{
			return false;
		}

		RegisterVisibleHoldSuccess(context.RuntimeState);
		EnsureVisibleHoldAnchor(context.RuntimeState, context.TargetPosition);
		int holdSectorIndex = GetSectorIndex(context.TargetPosition, context.AgentPosition);
		UpdateAngleClaim(context.TargetTransform, context.Agent.transform.GetInstanceID(), holdSectorIndex, position, context.AgentPosition, context.TargetPosition, context.RuntimeState);
		return true;
	}

	// When the target is visible but not holdable, find a better attack position.
	private bool TryFindVisibleAttackPosition(SenseContext context, out Vector3 position)
	{
		position = context.AgentPosition;
		if (!context.CanSeeTarget)
		{
			return false;
		}

		ResetVisibleHoldFailures(context.RuntimeState);
		ClearVisibleHoldAnchor(context.RuntimeState);
		if (TryGetBestRadialPoint(context.Agent, context.BodyState, context.TargetAimPosition, context.TargetTransform, context.TargetPosition, context.WeaponRange, context.RuntimeState, out Vector3 bestPoint))
		{
			position = bestPoint;
			return true;
		}

		if (TryGetBestPointOnCircle(context.TargetPosition, context.WeaponRange / 2f, context.Agent, context.RuntimeState, true, context.DistanceToTarget, out bestPoint))
		{
			position = bestPoint;
			return true;
		}

		position = context.RuntimeState.HasCachedPosition ? context.RuntimeState.CachedPosition : context.AgentPosition;
		return true;
	}

	// When the target is known but not visible, recover line of sight before falling back.
	private bool TryFindRecoveryAttackPosition(SenseContext context, out Vector3 position)
	{
		position = context.AgentPosition;
		if (context.CanSeeTarget)
		{
			return false;
		}

		ClearVisibleHoldAnchor(context.RuntimeState);
		if (TryGetBestRadialRecoveryPoint(context.Agent, context.BodyState, context.TargetAimPosition, context.TargetTransform, context.TargetPosition, context.WeaponRange, context.RuntimeState, out Vector3 recoveryPoint))
		{
			position = recoveryPoint;
			return true;
		}

		if (TryGetClosestStrafePoint(context.Agent, context.TargetPosition, context.RuntimeState, out Vector3 closestPoint))
		{
			LogFallbackSelection(context.Agent.transform, "StrafeFallback", closestPoint, context.TargetPosition);
			position = closestPoint;
			return true;
		}

		if (TryGetBestPointOnCircle(context.TargetPosition, context.DistanceToTarget, context.Agent, context.RuntimeState, false, float.PositiveInfinity, out Vector3 bestFallback))
		{
			LogFallbackSelection(context.Agent.transform, "CircleFallback", bestFallback, context.TargetPosition);
			position = bestFallback;
			return true;
		}

		if (context.RuntimeState.HasCachedPosition)
		{
			position = context.RuntimeState.CachedPosition;
			return true;
		}

		position = GetRandomPointOnCircle(context.TargetPosition, context.DistanceToTarget, context.Agent);
		return true;
	}

	// If the current target is gone, advance around the last known target position.
	private bool TryFindLostTargetAdvancePosition(SenseContext context, out Vector3 position, out Transform targetTransform)
	{
		position = context.AgentPosition;
		targetTransform = context.RuntimeState.LastTargetTransform;
		if (targetTransform == null)
		{
			return false;
		}

		return TryGetBestPointOnCircle(targetTransform.position, UnityEngine.Random.Range(1f, 5f), context.Agent, context.RuntimeState, false, float.PositiveInfinity, out position);
	}

	// With no current or remembered target, keep the old random nearby point behavior.
	private bool TryFindRandomIdlePosition(SenseContext context, out Vector3 position)
	{
		position = GetRandomPointOnCircle(context.AgentPosition, UnityEngine.Random.Range(1f, 5f), context.Agent);
		return true;
	}

	private PositionTarget CachePosition(SenseContext context, Vector3 position, Transform targetTransform)
	{
		return CachePosition(context.RuntimeState, context.Agent.transform.GetInstanceID(), position, context.AgentPosition, targetTransform);
	}

	private bool TryGetBestRadialRecoveryPoint(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out Vector3 bestPoint)
	{
		return TryGetBestRadialPoint(agent, bodyState, targetAimPosition, targetTransform, targetPosition, weaponRange, runtimeState, radialRecoveryMinDistanceMultiplier, radialRecoveryMaxDistanceMultiplier, out bestPoint);
	}

	private bool TryGetBestRadialPoint(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out Vector3 bestPoint)
	{
		return TryGetBestRadialPoint(agent, bodyState, targetAimPosition, targetTransform, targetPosition, weaponRange, runtimeState, radialMinDistanceMultiplier, radialMaxDistanceMultiplier, out bestPoint);
	}

	private bool TryGetBestRadialPoint(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, float minDistanceMultiplier, float maxDistanceMultiplier, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		if (targetTransform == null)
		{
			return false;
		}

		int agentId = agent.transform.GetInstanceID();
		int currentSector = GetSectorIndex(targetPosition, agent.transform.position);
		int attemptedMask = 0;
		CleanupClaimsForTarget(targetTransform, targetPosition, weaponRange);
		SharedAngleClaims.TryGetTargetState(targetTransform, out TargetAngleClaimState targetState);
		bool hasLockedSector = false;
		int lockedSectorIndex = -1;

		if (SharedAngleClaims.TryGetClaim(targetTransform, agentId, out AngleClaim claim) && claim.SectorIndex >= 0)
		{
			hasLockedSector = true;
			lockedSectorIndex = claim.SectorIndex;

			if (TryReuseClaimedPosition(agent, claim, targetPosition, out bestPoint))
			{
				ResetClaimedSectorFailures(runtimeState);
				UpdateAngleClaim(targetTransform, agentId, claim.SectorIndex, bestPoint, agent.transform.position, targetPosition, runtimeState);
				return true;
			}
		}
		else if (runtimeState.LastTargetTransform == targetTransform
			&& runtimeState.LastClaimedSectorIndex >= 0
			&& (!TryGetSectorOwnerClaim(targetState, runtimeState.LastClaimedSectorIndex, out AngleClaim lastSectorOwner) || lastSectorOwner.AgentId == agentId))
		{
			hasLockedSector = true;
			lockedSectorIndex = runtimeState.LastClaimedSectorIndex;
		}

		if (hasLockedSector)
		{
			if (TryRadialSector(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, weaponRange, runtimeState, lockedSectorIndex, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
			{
				ResetClaimedSectorFailures(runtimeState);
				return true;
			}

			RegisterClaimedSectorFailure(runtimeState);
			if (ShouldDelaySectorSwitch(runtimeState))
			{
				bestPoint = runtimeState.HasCachedPosition ? runtimeState.CachedPosition : agent.transform.position;
				return true;
			}
		}

		if (TryGetOpenRadialPoint(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
		{
			ResetClaimedSectorFailures(runtimeState);
			return true;
		}

		return false;
	}

	private void CleanupClaimsForTarget(Transform targetTransform, Vector3 targetPosition, float weaponRange)
	{
		if (!SharedAngleClaims.TryGetTargetState(targetTransform, out TargetAngleClaimState targetState))
		{
			return;
		}

		float targetMoveThresholdSqr = radialClaimCleanupTargetMoveThreshold * radialClaimCleanupTargetMoveThreshold;
		float maxRelevantDistance = Mathf.Max(weaponRange, weaponRange * radialClaimRelevanceDistanceMultiplier);
		float maxRelevantDistanceSqr = maxRelevantDistance * maxRelevantDistance;

		ClaimCleanupAgentIds.Clear();

		foreach (KeyValuePair<int, AngleClaim> entry in targetState.ClaimsByAgent)
		{
			AngleClaim claim = entry.Value;
			if (claim == null)
			{
				ClaimCleanupAgentIds.Add(entry.Key);
				continue;
			}

			if (Time.time > claim.ExpiresAt)
			{
				ClaimCleanupAgentIds.Add(entry.Key);
				continue;
			}

			if ((targetPosition - claim.LastKnownTargetPosition).sqrMagnitude > targetMoveThresholdSqr)
			{
				ClaimCleanupAgentIds.Add(entry.Key);
				continue;
			}

			if ((claim.LastKnownAgentPosition - targetPosition).sqrMagnitude > maxRelevantDistanceSqr)
			{
				ClaimCleanupAgentIds.Add(entry.Key);
			}
		}

		for (int i = 0; i < ClaimCleanupAgentIds.Count; i++)
		{
			targetState.ClaimsByAgent.Remove(ClaimCleanupAgentIds[i]);
		}

		if (targetState.ClaimsByAgent.Count == 0)
		{
			SharedAngleClaims.ClearTargetClaims(targetTransform);
		}
	}

	private bool TryReuseClaimedPosition(IMonoAgent agent, AngleClaim claim, Vector3 targetPosition, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		if (claim == null)
		{
			return false;
		}

		float targetShiftThresholdSqr = radialClaimReuseTargetMoveThreshold * radialClaimReuseTargetMoveThreshold;
		if ((targetPosition - claim.LastKnownTargetPosition).sqrMagnitude > targetShiftThresholdSqr)
		{
			return false;
		}

		float destinationReachedDistanceSqr = radialDestinationReachedDistance * radialDestinationReachedDistance;
		if ((agent.transform.position - claim.ClaimedPosition).sqrMagnitude <= destinationReachedDistanceSqr)
		{
			return false;
		}

		if (!TryResolveReachablePoint(agent, claim.ClaimedPosition, out Vector3 resolvedPoint))
		{
			return false;
		}

		if (!HasLineOfSight(resolvedPoint, targetPosition))
		{
			return false;
		}

		bestPoint = resolvedPoint;
		return true;
	}

	private bool TryHoldStableVisiblePosition(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out int sectorIndex)
	{
		sectorIndex = -1;
		if (agent == null || bodyState == null || targetTransform == null)
		{
			return false;
		}

		if (!SharedAngleClaims.TryGetClaim(targetTransform, agent.transform.GetInstanceID(), out AngleClaim claim)
			|| claim == null
			|| claim.SectorIndex < 0)
		{
			return false;
		}

		if (Vector3.Distance(agent.transform.position, targetPosition) > weaponRange)
		{
			return false;
		}

		float targetShiftThresholdSqr = radialClaimReuseTargetMoveThreshold * radialClaimReuseTargetMoveThreshold;
		if ((targetPosition - claim.LastKnownTargetPosition).sqrMagnitude > targetShiftThresholdSqr)
		{
			return false;
		}

		float destinationReachedDistanceSqr = radialDestinationReachedDistance * radialDestinationReachedDistance;
		if ((agent.transform.position - claim.ClaimedPosition).sqrMagnitude > destinationReachedDistanceSqr)
		{
			return false;
		}

		if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, agent.transform.position), targetAimPosition))
		{
			return false;
		}

		sectorIndex = claim.SectorIndex;
		return true;
	}

		private bool TryHoldAcceptableVisiblePosition(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, out int sectorIndex)
		{
		sectorIndex = -1;
		if (agent == null || bodyState == null || targetTransform == null)
		{
			return false;
		}

		if (Vector3.Distance(agent.transform.position, targetPosition) > weaponRange)
		{
			return false;
		}

		if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, agent.transform.position), targetAimPosition))
		{
			return false;
		}

		CleanupClaimsForTarget(targetTransform, targetPosition, weaponRange);
		SharedAngleClaims.TryGetTargetState(targetTransform, out TargetAngleClaimState targetState);

		sectorIndex = GetSectorIndex(targetPosition, agent.transform.position);
		if (TryGetSectorOwnerClaim(targetState, sectorIndex, out AngleClaim sectorOwner) && sectorOwner.AgentId != agent.transform.GetInstanceID())
		{
			return false;
		}

		float minClaimedDistanceSqr = GetNearestClaimedPositionDistanceSqr(targetState, agent.transform.GetInstanceID(), agent.transform.position);
		float minAllowedClaimDistanceSqr = tacticalClaimedPositionSeparation * tacticalClaimedPositionSeparation;
		if (minClaimedDistanceSqr < minAllowedClaimDistanceSqr)
		{
			return false;
		}

			return true;
		}

		private bool TryHoldVisiblePositionWithGrace(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out int sectorIndex)
		{
			sectorIndex = -1;
			if (agent == null || bodyState == null || targetTransform == null)
			{
				return false;
			}

			if (runtimeState.LastTargetTransform != targetTransform)
			{
				return false;
			}

			if (Vector3.Distance(agent.transform.position, targetPosition) > weaponRange)
			{
				return false;
			}

			if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, agent.transform.position), targetAimPosition))
			{
				return false;
			}

			if (SharedAngleClaims.TryGetClaim(targetTransform, agent.transform.GetInstanceID(), out AngleClaim claim)
				&& claim != null)
			{
				float targetShiftThresholdSqr = radialClaimReuseTargetMoveThreshold * radialClaimReuseTargetMoveThreshold;
				if ((targetPosition - claim.LastKnownTargetPosition).sqrMagnitude > targetShiftThresholdSqr)
				{
					return false;
				}
			}

			sectorIndex = GetSectorIndex(targetPosition, agent.transform.position);

			if (Time.time < runtimeState.VisibleHoldUntilTime)
			{
				return true;
			}

			RegisterVisibleHoldFailure(runtimeState);
			return runtimeState.ConsecutiveVisibleHoldFailures < Mathf.Max(1, visibleHoldFailureThreshold);
		}

		private void RegisterClaimedSectorFailure(AgentRuntimeState runtimeState)
		{
			runtimeState.ConsecutiveClaimedSectorFailures++;
		}

		private void ResetClaimedSectorFailures(AgentRuntimeState runtimeState)
		{
			runtimeState.ConsecutiveClaimedSectorFailures = 0;
		}

		private void RegisterVisibleHoldSuccess(AgentRuntimeState runtimeState)
		{
			runtimeState.VisibleHoldUntilTime = Time.time + visiblePositionDwellDuration;
			runtimeState.ConsecutiveVisibleHoldFailures = 0;
		}

		private void RegisterVisibleHoldFailure(AgentRuntimeState runtimeState)
		{
			runtimeState.ConsecutiveVisibleHoldFailures++;
		}

		private void ResetVisibleHoldFailures(AgentRuntimeState runtimeState)
		{
			runtimeState.ConsecutiveVisibleHoldFailures = 0;
			runtimeState.VisibleHoldUntilTime = 0f;
		}

		private bool ShouldBreakVisibleHoldFromTargetShift(AgentRuntimeState runtimeState, Vector3 targetPosition)
		{
			if (!runtimeState.HasVisibleHoldAnchor)
			{
				return false;
			}

			float targetShiftThresholdSqr = radialClaimReuseTargetMoveThreshold * radialClaimReuseTargetMoveThreshold;
			return (targetPosition - runtimeState.VisibleHoldAnchorTargetPosition).sqrMagnitude > targetShiftThresholdSqr;
		}

		private void EnsureVisibleHoldAnchor(AgentRuntimeState runtimeState, Vector3 targetPosition)
		{
			if (runtimeState.HasVisibleHoldAnchor)
			{
				return;
			}

			runtimeState.HasVisibleHoldAnchor = true;
			runtimeState.VisibleHoldAnchorTargetPosition = targetPosition;
		}

		private void ClearVisibleHoldAnchor(AgentRuntimeState runtimeState)
		{
			runtimeState.HasVisibleHoldAnchor = false;
			runtimeState.VisibleHoldAnchorTargetPosition = Vector3.zero;
		}

		private bool ShouldDelaySectorSwitch(AgentRuntimeState runtimeState)
		{
		if (Time.time < runtimeState.NextAllowedSectorSwitchTime)
		{
			return true;
		}

		return runtimeState.ConsecutiveClaimedSectorFailures < Mathf.Max(1, radialSectorFailureThreshold);
	}

	private bool TryGetOpenRadialPoint(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int currentSector, int agentId, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		bool searchPositiveDirectionFirst = ShouldSearchPositiveDirectionFirst(agentId);
		int preferredSector = GetPreferredSectorIndex(agentId);
		int exclusionRadius = Mathf.Max(0, radialClaimSectorExclusionRadius);

		if (TryGetOpenRadialPointWithExclusion(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, preferredSector, searchPositiveDirectionFirst, exclusionRadius, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
		{
			return true;
		}

		if (exclusionRadius <= 0)
		{
			return false;
		}

		return TryGetOpenRadialPointWithExclusion(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, preferredSector, searchPositiveDirectionFirst, 0, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint);
	}

	private bool TryGetOpenRadialPointWithExclusion(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int currentSector, int agentId, int preferredSector, bool searchPositiveDirectionFirst, int exclusionRadius, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;

		for (int i = 0; i < RadialSectorCount; i++)
		{
			if (!TryGetBestOpenSectorCandidate(targetState, currentSector, preferredSector, agentId, attemptedMask, searchPositiveDirectionFirst, exclusionRadius, out int candidateSector))
			{
				break;
			}

			if (TryRadialSector(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, weaponRange, runtimeState, candidateSector, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
			{
				return true;
			}
		}

		return false;
	}

	private bool TryGetBestOpenSectorCandidate(TargetAngleClaimState targetState, int currentSector, int preferredSector, int agentId, int attemptedMask, bool searchPositiveDirectionFirst, int exclusionRadius, out int bestSector)
	{
		bestSector = -1;
		int bestGapScore = -1;
		int bestGapCenterOffset = int.MaxValue;
		int bestPreferredDistance = int.MaxValue;
		int bestTravelDistance = int.MaxValue;
		int bestDirectionPriority = -1;

		for (int sectorIndex = 0; sectorIndex < RadialSectorCount; sectorIndex++)
		{
			int sectorMask = 1 << sectorIndex;
			if ((attemptedMask & sectorMask) != 0)
			{
				continue;
			}

			if (TryGetSectorOwnerClaim(targetState, sectorIndex, out AngleClaim sectorOwner) && sectorOwner.AgentId != agentId)
			{
				continue;
			}

			if (IsSectorWithinClaimExclusion(targetState, sectorIndex, exclusionRadius))
			{
				continue;
			}

			GetSectorGapMetrics(targetState, sectorIndex, out int gapScore, out int gapCenterOffset);
			int preferredDistance = GetWrappedSectorDistance(preferredSector, sectorIndex);
			int travelDistance = GetWrappedSectorDistance(currentSector, sectorIndex);
			int directionPriority = GetSectorDirectionPriority(currentSector, sectorIndex, searchPositiveDirectionFirst);

			if (gapScore > bestGapScore
				|| (gapScore == bestGapScore && gapCenterOffset < bestGapCenterOffset)
				|| (gapScore == bestGapScore && gapCenterOffset == bestGapCenterOffset && preferredDistance < bestPreferredDistance)
				|| (gapScore == bestGapScore && gapCenterOffset == bestGapCenterOffset && preferredDistance == bestPreferredDistance && travelDistance < bestTravelDistance)
				|| (gapScore == bestGapScore && gapCenterOffset == bestGapCenterOffset && preferredDistance == bestPreferredDistance && travelDistance == bestTravelDistance && directionPriority > bestDirectionPriority))
			{
				bestSector = sectorIndex;
				bestGapScore = gapScore;
				bestGapCenterOffset = gapCenterOffset;
				bestPreferredDistance = preferredDistance;
				bestTravelDistance = travelDistance;
				bestDirectionPriority = directionPriority;
			}
		}

		return bestSector >= 0;
	}

	private bool TryRadialSector(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int sectorIndex, int agentId, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		int sectorMask = 1 << sectorIndex;
		if ((attemptedMask & sectorMask) != 0)
		{
			return false;
		}

		if (TryGetSectorOwnerClaim(targetState, sectorIndex, out AngleClaim sectorOwner) && sectorOwner.AgentId != agentId)
		{
			return false;
		}

		attemptedMask |= sectorMask;

		if (!TryGetBestTacticalPoint(agent, bodyState, targetAimPosition, targetState, targetTransform, targetPosition, sectorIndex, agentId, weaponRange, minDistanceMultiplier, maxDistanceMultiplier, out bestPoint))
		{
			return false;
		}

		UpdateAngleClaim(targetTransform, agentId, sectorIndex, bestPoint, agent.transform.position, targetPosition, runtimeState);
		return true;
	}

	private bool TryGetBestTacticalPoint(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, int desiredSector, int agentId, float weaponRange, float minDistanceMultiplier, float maxDistanceMultiplier, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		float minDistance = Mathf.Max(1f, weaponRange * minDistanceMultiplier);
		float maxDistance = Mathf.Max(minDistance + 0.1f, weaponRange * maxDistanceMultiplier);
		TacticalCandidates.Clear();
		TacticalProbeDebugSnapshots.Clear();
		CollectSectorBandCandidates(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, minDistance, maxDistance);
		CollectLocalAgentCandidates(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, minDistance, maxDistance);
		if (enablePlayerAnnulusCandidates)
		{
			CollectPlayerAnnulusCandidates(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, minDistance, maxDistance);
		}
		TryAddIncumbentTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, minDistance, maxDistance);

		if (TacticalCandidates.Count == 0)
		{
			CaptureTacticalCandidateDebug(agent.transform, targetTransform, targetPosition, desiredSector, minDistance, maxDistance, -1, agent.transform.position, false);
			return false;
		}

		float bestScore = float.NegativeInfinity;
		bool found = false;
		int bestCandidateIndex = -1;
		float incumbentScore = float.NegativeInfinity;
		int incumbentCandidateIndex = -1;

		for (int i = 0; i < TacticalCandidates.Count; i++)
		{
			TacticalCandidate candidate = TacticalCandidates[i];
			float score = ScoreTacticalCandidate(candidate);
			if (candidate.IsIncumbent)
			{
				incumbentScore = score;
				incumbentCandidateIndex = i;
			}
			if (score > bestScore)
			{
				bestScore = score;
				bestPoint = candidate.Position;
				found = true;
				bestCandidateIndex = i;
			}
		}

		if (incumbentCandidateIndex >= 0
			&& bestCandidateIndex >= 0
			&& bestCandidateIndex != incumbentCandidateIndex
			&& bestScore < incumbentScore + tacticalSwitchScoreMargin)
		{
			bestCandidateIndex = incumbentCandidateIndex;
			bestPoint = TacticalCandidates[incumbentCandidateIndex].Position;
		}

		CaptureTacticalCandidateDebug(agent.transform, targetTransform, targetPosition, desiredSector, minDistance, maxDistance, bestCandidateIndex, bestPoint, found);
		return found;
	}

	private void CollectSectorBandCandidates(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, float minDistance, float maxDistance)
	{
		float midDistance = Mathf.Lerp(minDistance, maxDistance, 0.55f);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, desiredSector, 0f, minDistance, minDistance, maxDistance, 3);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, desiredSector, -0.22f, midDistance, minDistance, maxDistance, 3);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, desiredSector, 0.22f, midDistance, minDistance, maxDistance, 3);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, desiredSector, 0f, maxDistance, minDistance, maxDistance, 3);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, WrapSectorIndex(desiredSector - 1), 0f, midDistance, minDistance, maxDistance, 2);
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, WrapSectorIndex(desiredSector + 1), 0f, midDistance, minDistance, maxDistance, 2);
	}

	private void CollectLocalAgentCandidates(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, float minDistance, float maxDistance)
	{
		Vector3 desiredWorldPoint = targetPosition + GetSectorDirection(desiredSector) * Mathf.Lerp(minDistance, maxDistance, 0.5f);
		Vector3 moveDirection = desiredWorldPoint - agent.transform.position;
		if (moveDirection.sqrMagnitude < 0.0001f)
		{
			moveDirection = GetSectorDirection(desiredSector);
		}

		moveDirection.y = 0f;
		moveDirection.Normalize();
		Vector3 perpendicular = Vector3.Cross(Vector3.up, moveDirection);
		float innerRadius = Mathf.Max(0.5f, tacticalLocalSampleInnerRadius);
		float outerRadius = Mathf.Max(innerRadius + 0.5f, tacticalLocalSampleOuterRadius);
		float middleRadius = Mathf.Lerp(innerRadius, outerRadius, 0.55f);

		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, moveDirection, innerRadius, minDistance, maxDistance, 1);
		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, moveDirection, outerRadius, minDistance, maxDistance, 1);
		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, (moveDirection + perpendicular * 0.55f).normalized, middleRadius, minDistance, maxDistance, 1);
		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, (moveDirection - perpendicular * 0.55f).normalized, middleRadius, minDistance, maxDistance, 1);
		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, (moveDirection * 0.6f + perpendicular).normalized, outerRadius, minDistance, maxDistance, 0);
		TryAddLocalTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, (moveDirection * 0.6f - perpendicular).normalized, outerRadius, minDistance, maxDistance, 0);
	}

	private void CollectPlayerAnnulusCandidates(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, float minDistance, float maxDistance)
	{
		for (int i = 0; i < TacticalPlayerAnnulusSamples; i++)
		{
			int candidateSector = WrapSectorIndex(desiredSector + TacticalAnnulusSectorOffsets[i]);
			float radius = Mathf.Lerp(minDistance, maxDistance, TacticalAnnulusRadiusFractions[i]);
			float stableJitter = GetStableSigned(agentId, i, 11) * 0.08f;
			TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, candidateSector, TacticalAnnulusSectorFractions[i] + stableJitter, radius, minDistance, maxDistance, 2);
		}
	}

	private void TryAddLocalTacticalCandidate(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, Vector3 direction, float distance, float minDistance, float maxDistance, int sourcePriority)
	{
		if (direction.sqrMagnitude < 0.0001f)
		{
			return;
		}

		Vector3 rawPoint = agent.transform.position + direction.normalized * distance;
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, rawPoint, minDistance, maxDistance, sourcePriority);
	}

	private void TryAddTacticalCandidate(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, int sectorIndex, float sectorOffsetFraction, float radialDistance, float minDistance, float maxDistance, int sourcePriority)
	{
		Vector3 rawPoint = targetPosition + GetSectorDirection(sectorIndex, sectorOffsetFraction) * radialDistance;
		TryAddTacticalCandidate(agent, bodyState, targetAimPosition, targetState, targetPosition, desiredSector, agentId, rawPoint, minDistance, maxDistance, sourcePriority);
	}

	private void TryAddTacticalCandidate(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, Vector3 rawPoint, float minDistance, float maxDistance, int sourcePriority)
	{
		if (!TryResolveReachablePoint(agent, rawPoint, out Vector3 resolvedPoint))
		{
			RecordTacticalProbeDebug(rawPoint, TacticalProbeDebugResult.NavMeshRejected);
			return;
		}

		Vector3 probePoint = resolvedPoint;
		float distanceToTarget = Vector3.Distance(resolvedPoint, targetPosition);
		if (distanceToTarget < minDistance || distanceToTarget > maxDistance)
		{
			RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.DistanceRejected);
			return;
		}

		int candidateSector = GetSectorIndex(targetPosition, resolvedPoint);
		int sectorDistance = GetWrappedSectorDistance(desiredSector, candidateSector);
		if (sectorDistance > TacticalAllowedSectorOffset)
		{
			RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.SectorRejected);
			return;
		}

		if (TryGetSectorOwnerClaim(targetState, candidateSector, out AngleClaim sectorOwner) && sectorOwner.AgentId != agentId)
		{
			RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.OwnershipRejected);
			return;
		}

		if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, resolvedPoint), targetAimPosition))
		{
			RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.LineOfSightRejected);
			return;
		}

		float minClaimedDistanceSqr = GetNearestClaimedPositionDistanceSqr(targetState, agentId, resolvedPoint);
		float minAllowedClaimDistanceSqr = tacticalClaimedPositionSeparation * tacticalClaimedPositionSeparation;
		if (minClaimedDistanceSqr < minAllowedClaimDistanceSqr)
		{
			RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.ClaimSeparationRejected);
			return;
		}

		for (int i = 0; i < TacticalCandidates.Count; i++)
		{
				if ((TacticalCandidates[i].Position - resolvedPoint).sqrMagnitude < 0.25f)
				{
					RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.DedupedRejected);
					return;
				}
		}

		RecordTacticalProbeDebug(probePoint, TacticalProbeDebugResult.Accepted);

		GetSectorGapMetrics(targetState, candidateSector, out int gapScore, out int gapCenterOffset);
		TacticalCandidates.Add(new TacticalCandidate
		{
			Position = resolvedPoint,
			SectorIndex = candidateSector,
			SectorDistanceFromDesired = sectorDistance,
			GapScore = gapScore,
			GapCenterOffset = gapCenterOffset,
			DistanceToAgentSqr = (resolvedPoint - agent.transform.position).sqrMagnitude,
			MinClaimedDistanceSqr = minClaimedDistanceSqr,
			SourcePriority = sourcePriority,
			IsIncumbent = false,
		});
	}

	private void TryAddIncumbentTacticalCandidate(IMonoAgent agent, BodyState bodyState, Vector3 targetAimPosition, TargetAngleClaimState targetState, Vector3 targetPosition, int desiredSector, int agentId, float minDistance, float maxDistance)
	{
		if (!TryResolveReachablePoint(agent, agent.transform.position, out Vector3 resolvedPoint))
		{
			return;
		}

		float distanceToTarget = Vector3.Distance(resolvedPoint, targetPosition);
		if (distanceToTarget < minDistance || distanceToTarget > maxDistance)
		{
			return;
		}

		int candidateSector = GetSectorIndex(targetPosition, resolvedPoint);
		int sectorDistance = GetWrappedSectorDistance(desiredSector, candidateSector);
		if (sectorDistance > TacticalAllowedSectorOffset)
		{
			return;
		}

		if (TryGetSectorOwnerClaim(targetState, candidateSector, out AngleClaim sectorOwner) && sectorOwner.AgentId != agentId)
		{
			return;
		}

		if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, resolvedPoint), targetAimPosition))
		{
			return;
		}

		float minClaimedDistanceSqr = GetNearestClaimedPositionDistanceSqr(targetState, agentId, resolvedPoint);
		float minAllowedClaimDistanceSqr = tacticalClaimedPositionSeparation * tacticalClaimedPositionSeparation;
		if (minClaimedDistanceSqr < minAllowedClaimDistanceSqr)
		{
			return;
		}

		for (int i = 0; i < TacticalCandidates.Count; i++)
		{
			if ((TacticalCandidates[i].Position - resolvedPoint).sqrMagnitude < 0.25f)
			{
				TacticalCandidate candidate = TacticalCandidates[i];
				candidate.IsIncumbent = true;
				TacticalCandidates[i] = candidate;
				return;
			}
		}

		GetSectorGapMetrics(targetState, candidateSector, out int gapScore, out int gapCenterOffset);
		TacticalCandidates.Add(new TacticalCandidate
		{
			Position = resolvedPoint,
			SectorIndex = candidateSector,
			SectorDistanceFromDesired = sectorDistance,
			GapScore = gapScore,
			GapCenterOffset = gapCenterOffset,
			DistanceToAgentSqr = 0f,
			MinClaimedDistanceSqr = minClaimedDistanceSqr,
			SourcePriority = 4,
			IsIncumbent = true,
		});
	}

	private float GetNearestClaimedPositionDistanceSqr(TargetAngleClaimState targetState, int agentId, Vector3 position)
	{
		if (targetState == null || targetState.ClaimsByAgent.Count == 0)
		{
			return float.PositiveInfinity;
		}

		float nearestDistanceSqr = float.PositiveInfinity;

		foreach (AngleClaim claim in targetState.ClaimsByAgent.Values)
		{
			if (claim == null || claim.AgentId == agentId)
			{
				continue;
			}

			float distanceSqr = (claim.ClaimedPosition - position).sqrMagnitude;
			if (distanceSqr < nearestDistanceSqr)
			{
				nearestDistanceSqr = distanceSqr;
			}
		}

		return nearestDistanceSqr;
	}

	private Vector3 GetCandidateLineOfSightOrigin(BodyState bodyState, Vector3 candidatePosition)
	{
		float eyeHeight = 1.5f;
		if (bodyState != null && bodyState.headCollider != null)
		{
			eyeHeight = Mathf.Max(0.5f, bodyState.headCollider.bounds.center.y - bodyState.transform.position.y);
		}

		return candidatePosition + Vector3.up * eyeHeight;
	}

	private float ScoreTacticalCandidate(TacticalCandidate candidate)
	{
		float score = candidate.SectorDistanceFromDesired == 0 ? tacticalDesiredSectorScore : tacticalNearbySectorScore;
		score += candidate.GapScore * tacticalGapScoreWeight;
		score -= candidate.GapCenterOffset * tacticalGapCenterPenaltyWeight;
		score += candidate.SourcePriority * tacticalSourcePriorityWeight;
		score += float.IsPositiveInfinity(candidate.MinClaimedDistanceSqr)
			? tacticalUnclaimedPositionBonus
			: Mathf.Min(Mathf.Sqrt(candidate.MinClaimedDistanceSqr), tacticalClaimedSeparationBonusMaxDistance) * tacticalClaimedSeparationBonusWeight;
		score -= Mathf.Sqrt(candidate.DistanceToAgentSqr) * tacticalNearbyPositionPenaltyWeight;
		if (candidate.IsIncumbent)
		{
			score += tacticalCurrentPositionStickinessBonus;
		}
		return score;
	}

	private void RecordTacticalProbeDebug(Vector3 position, TacticalProbeDebugResult result)
	{
		TacticalProbeDebugSnapshots.Add(new TacticalProbeDebugSnapshot
		{
			Position = position,
			Result = result,
		});
	}

	private void CaptureTacticalCandidateDebug(Transform agentTransform, Transform targetTransform, Vector3 targetPosition, int desiredSector, float minDistance, float maxDistance, int selectedCandidateIndex, Vector3 bestPoint, bool foundBestPoint)
	{
		if (!drawTacticalQueryGizmos && !spawnTacticalQueryDebugObjects)
		{
			TacticalCandidateDebugSnapshots.Clear();
			HasLastTacticalDebugBestPoint = false;
			UpdateRuntimeTacticalDebugObjects();
			return;
		}

		if (!ShouldCaptureTacticalDebug(agentTransform))
		{
			return;
		}

		LastTacticalDebugAgent = agentTransform;
		LastTacticalDebugTarget = targetTransform;
		LastTacticalDebugTargetPosition = targetPosition;
		LastTacticalDebugDesiredSector = desiredSector;
		LastTacticalDebugMinDistance = minDistance;
		LastTacticalDebugMaxDistance = maxDistance;
		LastTacticalDebugBestPoint = bestPoint;
		HasLastTacticalDebugBestPoint = foundBestPoint;
		TacticalCandidateDebugSnapshots.Clear();

		for (int i = 0; i < TacticalCandidates.Count; i++)
		{
			TacticalCandidate candidate = TacticalCandidates[i];
			TacticalCandidateDebugSnapshots.Add(new TacticalCandidateDebugSnapshot
			{
				Position = candidate.Position,
				SourcePriority = candidate.SourcePriority,
				IsSelected = i == selectedCandidateIndex,
			});
		}

		if (!HasLoggedTacticalDebugCapture)
		{
			HasLoggedTacticalDebugCapture = true;
			Debug.Log($"HostileTargetSensor: captured tactical query for '{agentTransform?.name}' with {TacticalCandidateDebugSnapshots.Count} candidates from {TacticalProbeDebugSnapshots.Count} probes.");
		}

		if (logTacticalQuerySelections && foundBestPoint)
		{
			bool selectionChanged = !HasLastLoggedTacticalSelection
				|| (LastLoggedTacticalSelectionPoint - bestPoint).sqrMagnitude > 0.01f;
			if (selectionChanged)
			{
				HasLastLoggedTacticalSelection = true;
				LastLoggedTacticalSelectionPoint = bestPoint;
				float distanceToTarget = Vector3.Distance(bestPoint, targetPosition);
				Debug.Log($"HostileTargetSensor: tactical candidate selected for '{agentTransform?.name}' at {bestPoint} (distance {distanceToTarget:F2}) from {TacticalCandidateDebugSnapshots.Count} candidates / {TacticalProbeDebugSnapshots.Count} probes.");
			}
		}

		UpdateRuntimeTacticalDebugObjects();
	}

	private Color GetTacticalCandidateGizmoColor(TacticalCandidateDebugSnapshot snapshot)
	{
		if (snapshot.IsSelected)
		{
			return Color.green;
		}

		switch (snapshot.SourcePriority)
		{
			case 3:
				return new Color(1f, 0.8f, 0.2f);
			case 2:
				return new Color(1f, 0.25f, 0.85f);
			case 1:
				return Color.cyan;
			default:
				return new Color(0.5f, 0.7f, 1f);
		}
	}

	private Color GetTacticalProbeDebugColor(TacticalProbeDebugResult result)
	{
		switch (result)
		{
			case TacticalProbeDebugResult.Accepted:
				return new Color(0.2f, 1f, 0.35f, 1f);
			case TacticalProbeDebugResult.NavMeshRejected:
				return Color.gray;
			case TacticalProbeDebugResult.DistanceRejected:
				return new Color(1f, 0.55f, 0.15f, 1f);
			case TacticalProbeDebugResult.SectorRejected:
				return new Color(1f, 0.8f, 0.1f, 1f);
			case TacticalProbeDebugResult.OwnershipRejected:
				return new Color(0.8f, 0.2f, 1f, 1f);
			case TacticalProbeDebugResult.LineOfSightRejected:
				return Color.red;
			case TacticalProbeDebugResult.ClaimSeparationRejected:
				return new Color(0.15f, 0.5f, 1f, 1f);
			case TacticalProbeDebugResult.DedupedRejected:
				return new Color(1f, 0.4f, 0.7f, 1f);
			default:
				return Color.white;
		}
	}

	private string GetTacticalProbeDebugLabel(TacticalProbeDebugResult result, bool isChosen)
	{
		if (isChosen)
		{
			return "ChosenCandidate";
		}

		switch (result)
		{
			case TacticalProbeDebugResult.Accepted:
				return "Accepted";
			case TacticalProbeDebugResult.NavMeshRejected:
				return "Rejected_NavMesh";
			case TacticalProbeDebugResult.DistanceRejected:
				return "Rejected_Distance";
			case TacticalProbeDebugResult.SectorRejected:
				return "Rejected_Sector";
			case TacticalProbeDebugResult.OwnershipRejected:
				return "Rejected_Ownership";
			case TacticalProbeDebugResult.LineOfSightRejected:
				return "Rejected_LineOfSight";
			case TacticalProbeDebugResult.ClaimSeparationRejected:
				return "Rejected_ClaimSeparation";
			case TacticalProbeDebugResult.DedupedRejected:
				return "Rejected_Deduped";
			default:
				return "Rejected_Unknown";
		}
	}

	private string GetTacticalCandidateDebugLabel(TacticalCandidateDebugSnapshot snapshot)
	{
		if (snapshot.IsSelected)
		{
			return "SelectedCandidate";
		}

		switch (snapshot.SourcePriority)
		{
			case 3:
				return "Candidate_SectorBand";
			case 2:
				return "Candidate_PlayerAnnulus";
			case 1:
				return "Candidate_LocalBiased";
			default:
				return "Candidate_LocalWide";
		}
	}

	private bool ShouldCaptureTacticalDebug(Transform agentTransform)
	{
		if (agentTransform == null || string.IsNullOrEmpty(tacticalDebugAgentName))
		{
			return true;
		}

		return agentTransform.name == tacticalDebugAgentName
			|| agentTransform.name.IndexOf(tacticalDebugAgentName, System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	private void LogFallbackSelection(Transform agentTransform, string fallbackName, Vector3 point, Vector3 targetPosition)
	{
		if (!logFallbackSelections || !ShouldCaptureTacticalDebug(agentTransform))
		{
			return;
		}

		float distanceToTarget = Vector3.Distance(point, targetPosition);
		Debug.Log($"HostileTargetSensor: {fallbackName} selected for '{agentTransform?.name}' at {point} (distance {distanceToTarget:F2}) toward target {targetPosition}.");
	}

	private void UpdateRuntimeTacticalDebugObjects()
	{
		if (!spawnTacticalQueryDebugObjects || !Application.isPlaying)
		{
			SetRuntimeDebugObjectsActive(false);
			return;
		}

		Transform targetTransform = LastTacticalDebugTarget;
		if (targetTransform == null && !string.IsNullOrEmpty(tacticalDebugTargetName))
		{
			GameObject targetObject = GameObject.Find(tacticalDebugTargetName);
			if (targetObject != null)
			{
				targetTransform = targetObject.transform;
			}
		}

		if (targetTransform == null)
		{
			SetRuntimeDebugObjectsActive(false);
			return;
		}

		EnsureRuntimeDebugRoot();
		TacticalDebugRoot.SetActive(true);
		LastTacticalDebugTarget = targetTransform;
		LastTacticalDebugTargetPosition = targetTransform.position;
		UpdateRingDebugObjects(TacticalDebugInnerRingObjects, LastTacticalDebugMinDistance, new Color(1f, 0.55f, 0.15f, 1f));
		UpdateRingDebugObjects(TacticalDebugOuterRingObjects, LastTacticalDebugMaxDistance, new Color(1f, 0.2f, 0.15f, 1f));
		EnsureDebugObjectCount(TacticalDebugProbeObjects, TacticalProbeDebugSnapshots.Count, "Probe", PrimitiveType.Cube);
		EnsureDebugObjectCount(TacticalDebugCandidateObjects, TacticalCandidateDebugSnapshots.Count, "Candidate", PrimitiveType.Sphere);

		for (int i = 0; i < TacticalDebugProbeObjects.Count; i++)
		{
			GameObject marker = TacticalDebugProbeObjects[i];
			if (i >= TacticalProbeDebugSnapshots.Count)
			{
				marker.SetActive(false);
				continue;
			}

			TacticalProbeDebugSnapshot snapshot = TacticalProbeDebugSnapshots[i];
			float scale = snapshot.Result == TacticalProbeDebugResult.Accepted
				? tacticalDebugCandidateMarkerScale * 0.75f
				: tacticalDebugCandidateMarkerScale * 0.5f;
			bool isChosen = HasLastTacticalDebugBestPoint
				&& snapshot.Result == TacticalProbeDebugResult.Accepted
				&& (snapshot.Position - LastTacticalDebugBestPoint).sqrMagnitude < 0.01f;
			marker.name = $"TacticalDebug_Probe_{i}_{GetTacticalProbeDebugLabel(snapshot.Result, isChosen)}";
			ConfigureDebugObject(marker, snapshot.Position + Vector3.up * 0.15f, scale, GetTacticalProbeDebugColor(snapshot.Result));
		}

		for (int i = 0; i < TacticalDebugCandidateObjects.Count; i++)
		{
			GameObject marker = TacticalDebugCandidateObjects[i];
			if (i >= TacticalCandidateDebugSnapshots.Count)
			{
				marker.SetActive(false);
				continue;
			}

			TacticalCandidateDebugSnapshot snapshot = TacticalCandidateDebugSnapshots[i];
			float scale = snapshot.IsSelected ? tacticalDebugCandidateMarkerScale * 1.5f : tacticalDebugCandidateMarkerScale;
			marker.name = $"TacticalDebug_Candidate_{i}_{GetTacticalCandidateDebugLabel(snapshot)}";
			ConfigureDebugObject(marker, snapshot.Position, scale, GetTacticalCandidateGizmoColor(snapshot));
		}
	}

	private void UpdateRingDebugObjects(List<GameObject> ringObjects, float radius, Color color)
	{
		int markerCount = Mathf.Max(8, tacticalDebugRingMarkerCount);
		EnsureDebugObjectCount(ringObjects, markerCount, "Ring", PrimitiveType.Sphere);

		for (int i = 0; i < ringObjects.Count; i++)
		{
			GameObject marker = ringObjects[i];
			float angle = i / (float)markerCount * Mathf.PI * 2f;
			Vector3 position = LastTacticalDebugTargetPosition + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
			ConfigureDebugObject(marker, position, tacticalDebugRingMarkerScale, color);
		}
	}

	private void EnsureRuntimeDebugRoot()
	{
		if (TacticalDebugRoot != null)
		{
			return;
		}

		TacticalDebugRoot = new GameObject("TacticalQueryDebug");
	}

	private void EnsureDebugObjectCount(List<GameObject> pool, int desiredCount, string label, PrimitiveType primitiveType)
	{
		while (pool.Count < desiredCount)
		{
			GameObject marker = GameObject.CreatePrimitive(primitiveType);
			marker.name = $"TacticalDebug_{label}_{pool.Count}";
			marker.transform.SetParent(TacticalDebugRoot != null ? TacticalDebugRoot.transform : null, false);
			Collider markerCollider = marker.GetComponent<Collider>();
			if (markerCollider != null)
			{
				Object.Destroy(markerCollider);
			}

			pool.Add(marker);
		}
	}

	private void ConfigureDebugObject(GameObject marker, Vector3 position, float scale, Color color)
	{
		if (marker == null)
		{
			return;
		}

		marker.SetActive(true);
		marker.transform.position = position;
		marker.transform.localScale = Vector3.one * scale;
		Renderer renderer = marker.GetComponent<Renderer>();
		if (renderer != null)
		{
			renderer.material.color = color;
		}
	}

	private void SetRuntimeDebugObjectsActive(bool isActive)
	{
		if (TacticalDebugRoot != null)
		{
			TacticalDebugRoot.SetActive(isActive);
		}
	}

	private bool TryGetSectorOwnerClaim(TargetAngleClaimState targetState, int sectorIndex, out AngleClaim ownerClaim)
	{
		ownerClaim = null;
		if (targetState == null)
		{
			return false;
		}

		foreach (AngleClaim claim in targetState.ClaimsByAgent.Values)
		{
			if (claim == null)
			{
				continue;
			}

			if (claim.SectorIndex == sectorIndex)
			{
				ownerClaim = claim;
				return true;
			}
		}

		return false;
	}

	private void GetSectorGapMetrics(TargetAngleClaimState targetState, int sectorIndex, out int gapScore, out int gapCenterOffset)
	{
		if (targetState == null || targetState.ClaimsByAgent.Count == 0)
		{
			gapScore = RadialSectorCount * 2;
			gapCenterOffset = 0;
			return;
		}

		int clockwiseDistance = RadialSectorCount;
		int counterClockwiseDistance = RadialSectorCount;

		foreach (AngleClaim claim in targetState.ClaimsByAgent.Values)
		{
			if (claim == null)
			{
				continue;
			}

			int clockwiseOffset = GetClockwiseSectorDistance(sectorIndex, claim.SectorIndex);
			int counterClockwiseOffset = GetClockwiseSectorDistance(claim.SectorIndex, sectorIndex);
			clockwiseDistance = Mathf.Min(clockwiseDistance, clockwiseOffset);
			counterClockwiseDistance = Mathf.Min(counterClockwiseDistance, counterClockwiseOffset);
		}

		gapScore = clockwiseDistance + counterClockwiseDistance;
		gapCenterOffset = Mathf.Abs(clockwiseDistance - counterClockwiseDistance);
	}

	private int GetWrappedSectorDistance(int firstSector, int secondSector)
	{
		int delta = Mathf.Abs(WrapSectorIndex(firstSector) - WrapSectorIndex(secondSector));
		return Mathf.Min(delta, RadialSectorCount - delta);
	}

	private bool IsSectorWithinClaimExclusion(TargetAngleClaimState targetState, int sectorIndex, int exclusionRadius)
	{
		if (targetState == null || exclusionRadius <= 0)
		{
			return false;
		}

		foreach (AngleClaim claim in targetState.ClaimsByAgent.Values)
		{
			if (claim == null)
			{
				continue;
			}

			if (GetWrappedSectorDistance(sectorIndex, claim.SectorIndex) <= exclusionRadius)
			{
				return true;
			}
		}

		return false;
	}

	private int GetClockwiseSectorDistance(int fromSector, int toSector)
	{
		return WrapSectorIndex(toSector - fromSector);
	}

	private int GetPreferredSectorIndex(int agentId)
	{
		long hash = (long)agentId * 73856093L;
		long positiveHash = hash < 0 ? -hash : hash;
		return WrapSectorIndex((int)(positiveHash % RadialSectorCount));
	}

	private float GetStableSigned(int agentId, int sampleIndex, int salt)
	{
		long hash = (long)agentId * 73856093L + (long)sampleIndex * 19349663L + salt * 83492791L;
		long positiveHash = hash < 0 ? -hash : hash;
		return (positiveHash % 1000L) / 999f * 2f - 1f;
	}

	private int GetSectorDirectionPriority(int currentSector, int sectorIndex, bool searchPositiveDirectionFirst)
	{
		if (sectorIndex == currentSector)
		{
			return 1;
		}

		int positiveOffset = WrapSectorIndex(sectorIndex - currentSector);
		int negativeOffset = WrapSectorIndex(currentSector - sectorIndex);
		bool isPositiveDirection = positiveOffset <= negativeOffset;
		return isPositiveDirection == searchPositiveDirectionFirst ? 1 : 0;
	}

	private void UpdateAngleClaim(Transform targetTransform, int agentId, int sectorIndex, Vector3 claimedPosition, Vector3 agentPosition, Vector3 targetPosition, AgentRuntimeState runtimeState)
	{
		AngleClaim claim = SharedAngleClaims.GetOrCreateClaim(targetTransform, agentId);
		if (claim == null)
		{
			return;
		}

		if (runtimeState.LastClaimedSectorIndex != sectorIndex)
		{
			runtimeState.NextAllowedSectorSwitchTime = Time.time + radialSectorSwitchCooldown;
			runtimeState.ConsecutiveClaimedSectorFailures = 0;
		}

		claim.SectorIndex = sectorIndex;
		claim.LastUpdatedTime = Time.time;
		claim.ExpiresAt = Time.time + radialClaimDuration;
		claim.ClaimedPosition = claimedPosition;
		claim.LastKnownAgentPosition = agentPosition;
		claim.LastKnownTargetPosition = targetPosition;
		runtimeState.LastClaimedSectorIndex = sectorIndex;
	}

	private int GetSectorIndex(Vector3 center, Vector3 position)
	{
		Vector2 direction = new Vector2(position.x - center.x, position.z - center.z);
		if (direction.sqrMagnitude < 0.0001f)
		{
			return 0;
		}

		float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
		if (angle < 0f)
		{
			angle += 360f;
		}

		float sectorSize = 360f / RadialSectorCount;
		return Mathf.Clamp(Mathf.FloorToInt(angle / sectorSize), 0, RadialSectorCount - 1);
	}

	private Vector3 GetSectorDirection(int sectorIndex)
	{
		return GetSectorDirection(sectorIndex, 0f);
	}

	private Vector3 GetSectorDirection(int sectorIndex, float sectorOffsetFraction)
	{
		float sectorSize = 360f / RadialSectorCount;
		float angle = (WrapSectorIndex(sectorIndex) + 0.5f + sectorOffsetFraction) * sectorSize * Mathf.Deg2Rad;
		return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
	}

	private int WrapSectorIndex(int sectorIndex)
	{
		int wrapped = sectorIndex % RadialSectorCount;
		if (wrapped < 0)
		{
			wrapped += RadialSectorCount;
		}

		return wrapped;
	}

	private bool ShouldSearchPositiveDirectionFirst(int agentId)
	{
		return (Mathf.Abs(agentId) & 1) == 0;
	}

	private bool TryGetBestPointOnCircle(Vector3 center, float radius, IMonoAgent agent, AgentRuntimeState runtimeState, bool requireLineOfSight, float maxDistanceFromAgent, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		int pointCount = Mathf.Min(numberOfPoints, MaxCirclePointSamples);
		if (pointCount <= 0)
			return false;

		float searchRadius = Mathf.Max(radius * allySearchRadiusMultiplier, 1f);
		RefreshAllyPositions(center, agent.transform, searchRadius);

		float angleStep = 360f / pointCount;
		float angleOffset = GetAgentAngleOffset(agent);
		float bestScore = float.NegativeInfinity;
		bool found = false;
		int samplesToCheck = Mathf.Min(pointCount, CirclePointsPerSense);
		int startIndex = runtimeState.CirclePointStartIndex;

		for (int i = 0; i < samplesToCheck; i++)
		{
			int index = (startIndex + i) % pointCount;
			float angle = angleOffset + index * angleStep;
			Vector3 raw = center + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;

			if (!TryResolveReachablePoint(agent, raw, out Vector3 candidate))
				continue;
			float distanceToAgent = Vector3.Distance(agent.transform.position, candidate);
			if (distanceToAgent > maxDistanceFromAgent)
				continue;

			if (requireLineOfSight && !HasLineOfSight(candidate, center))
				continue;

			float score = -distanceToAgent * distancePenaltyWeight;
			score -= GetAllyAnglePenalty(candidate, center);

			if (score > bestScore)
			{
				bestScore = score;
				bestPoint = candidate;
				found = true;
			}
		}

		runtimeState.CirclePointStartIndex = (startIndex + samplesToCheck) % pointCount;

		if (found)
			return true;

		return TryGetBestPointAroundAgent(agent, center, runtimeState, UnityEngine.Random.Range(0.2f, 0.5f), out bestPoint);
	}

	private bool TryGetBestPointAroundAgent(IMonoAgent agent, Vector3 playerPosition, AgentRuntimeState runtimeState, float maxDistanceFromAgent, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		int fallbackPointCount = Mathf.Min(agentFallbackPoints, MaxFallbackPointSamples);
		if (fallbackPointCount <= 0 || agentFallbackRadius <= 0f)
			return false;

		float angleStep = 360f / fallbackPointCount;
		float angleOffset = GetAgentAngleOffset(agent);
		float bestScore = float.NegativeInfinity;
		bool found = false;
		int samplesToCheck = Mathf.Min(fallbackPointCount, FallbackPointsPerSense);
		int startIndex = runtimeState.FallbackPointStartIndex;

		for (int i = 0; i < samplesToCheck; i++)
		{
			int index = (startIndex + i) % fallbackPointCount;
			float angle = angleOffset + index * angleStep;
			Vector3 raw = agent.transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0f, Mathf.Sin(angle * Mathf.Deg2Rad)) * agentFallbackRadius;

			if (!TryResolveReachablePoint(agent, raw, out Vector3 candidate))
				continue;
			float distanceToAgent = Vector3.Distance(agent.transform.position, candidate);
			if (distanceToAgent > maxDistanceFromAgent)
				continue;

			float distanceToPlayer = Vector3.Distance(candidate, playerPosition);
			float score = -distanceToPlayer * agentFallbackPlayerWeight - distanceToAgent * distancePenaltyWeight;
			score -= GetAllyAnglePenalty(candidate, playerPosition);

			if (score > bestScore)
			{
				bestScore = score;
				bestPoint = candidate;
				found = true;
			}
		}

		runtimeState.FallbackPointStartIndex = (startIndex + samplesToCheck) % fallbackPointCount;

		return found;
	}

	private bool TryGetClosestStrafePoint(IMonoAgent agent, Vector3 targetPosition, AgentRuntimeState runtimeState, out Vector3 closestPoint)
	{
		closestPoint = Vector3.zero;
		float closestDistance = float.MaxValue;
		float lineLength = 20f;
		Vector3 direction = Vector3.Cross(Vector3.up, (targetPosition - agent.transform.position).normalized);
		int totalPoints = MaxStrafePointSamples;
		int samplesToCheck = Mathf.Min(totalPoints, StrafePointsPerSense);
		int startIndex = (runtimeState.StrafePointStartIndex + GetStrafeSampleOffset(agent.transform.GetInstanceID(), totalPoints)) % totalPoints;
		bool searchPositiveDirectionFirst = ShouldSearchPositiveDirectionFirst(agent.transform.GetInstanceID());

		for (int i = 0; i < samplesToCheck; i++)
		{
			int sampleOffset = searchPositiveDirectionFirst
				? i
				: -i;
			int index = (startIndex + sampleOffset + totalPoints) % totalPoints;
			float t = totalPoints <= 1 ? 0.5f : (float)index / (totalPoints - 1);
			Vector3 point = agent.transform.position + new Vector3(0f, 2f, 0f) + direction * (t * lineLength - lineLength / 2f);
			if (!TryResolveReachablePoint(agent, point, out Vector3 resolvedPoint))
			{
				continue;
			}

			float distanceToAgent = Vector3.Distance(resolvedPoint, agent.transform.position);

			BodyState bodyState = agent.GetComponentInChildren<BodyState>();
			if (!HasLineOfSight(GetCandidateLineOfSightOrigin(bodyState, resolvedPoint), targetPosition))
			{
				continue;
			}

			if (distanceToAgent < closestDistance)
			{
				closestDistance = distanceToAgent;
				closestPoint = resolvedPoint;
			}
		}

		runtimeState.StrafePointStartIndex = (startIndex + samplesToCheck) % totalPoints;
		return closestPoint != Vector3.zero;
	}

	private int GetStrafeSampleOffset(int agentId, int totalPoints)
	{
		if (totalPoints <= 0)
		{
			return 0;
		}

		long hash = (long)agentId * 19349663L;
		long positiveHash = hash < 0 ? -hash : hash;
		return (int)(positiveHash % totalPoints);
	}

	private void RefreshAllyPositions(Vector3 center, Transform self, float searchRadius)
	{
		AllyPositions.Clear();
		AllyRoots.Clear();

		int count = Physics.OverlapSphereNonAlloc(center, searchRadius, AllyColliders, AttackConfig.AllyLayerMask);
		for (int i = 0; i < count; i++)
		{
			var col = AllyColliders[i];
			if (col == null)
				continue;

			var colBodyState = col.GetComponent<BodyState>();
			if (colBodyState == null)
				continue;

			if (!(colBodyState.TimeToAim < colBodyState.AttackConfig.TimeToAim) || colBodyState.isDead)
			{
				continue;
			}

			Transform root = col.transform.root;
			if (root == self.root)
				continue;

			bool alreadyAdded = false;
			for (int j = 0; j < AllyRoots.Count; j++)
			{
				if (AllyRoots[j] == root)
				{
					alreadyAdded = true;
					break;
				}
			}

			if (alreadyAdded)
				continue;

			AllyRoots.Add(root);
			AllyPositions.Add(root.position);
		}
	}

	private float GetAllyAnglePenalty(Vector3 candidate, Vector3 center)
	{
		if (AllyPositions.Count == 0 || minAllySeparationAngle <= 0f || allySeparationWeight <= 0f)
			return 0f;

		Vector2 candidateDir = new Vector2(candidate.x - center.x, candidate.z - center.z);
		if (candidateDir.sqrMagnitude < 0.0001f)
			return 0f;

		candidateDir.Normalize();
		float penalty = 0f;

		for (int i = 0; i < AllyPositions.Count; i++)
		{
			Vector2 allyDir = new Vector2(AllyPositions[i].x - center.x, AllyPositions[i].z - center.z);
			if (allyDir.sqrMagnitude < 0.0001f)
				continue;

			allyDir.Normalize();
			float dot = Mathf.Clamp(Vector2.Dot(candidateDir, allyDir), -1f, 1f);
			float angle = Mathf.Acos(dot) * Mathf.Rad2Deg;

			if (angle < minAllySeparationAngle)
			{
				float t = 1f - (angle / minAllySeparationAngle);
				penalty += t * t * allySeparationWeight;
			}
		}

		return penalty;
	}

	private float GetAgentAngleOffset(IMonoAgent agent)
	{
		int id = Mathf.Abs(agent.transform.GetInstanceID());
		return (id % 1000) * 0.01f;
	}

	private Vector3 GetRandomPointOnCircle(Vector3 center, float radius, IMonoAgent agent)
	{
		// // Generate a random angle between 0 and 2π
		// float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);

		// // Calculate the x and z coordinates of the random point on the circle
		// float x = center.x + radius * Mathf.Cos(randomAngle);
		// float z = center.z + radius * Mathf.Sin(randomAngle);

		// // Set the y coordinate to the center's y coordinate (assuming the circle is on the same plane)
		// float y = center.y;

		// if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1, NavMesh.AllAreas))
		// {
		// 	return hit.position;
		// }

		// Return the random point on the circle
		// return new Vector3(x, y, z);

		int count = 0;

		while (count < MaxRandomCircleAttempts)
		{
			// Generate a random angle between 0 and 2π
			float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);

			// Calculate the x and z coordinates of the random point on the circle
			float x = center.x + radius * Mathf.Cos(randomAngle);
			float z = center.z + radius * Mathf.Sin(randomAngle);

			// Set the y coordinate to the center's y coordinate (assuming the circle is on the same plane)
			float y = center.y;

			Vector3 position = new Vector3(x, y, z);

			if (TryResolveReachablePoint(agent, position, out Vector3 reachablePoint))
			{
				return reachablePoint;
			}

			count++;
		}

		return agent.transform.position;
	}

	private bool TryResolveReachablePoint(IMonoAgent agent, Vector3 rawPoint, out Vector3 reachablePoint)
	{
		reachablePoint = agent.transform.position;
		NavMeshAgent navMeshAgent = agent.GetComponent<NavMeshAgent>();
		int areaMask = navMeshAgent != null ? navMeshAgent.areaMask : NavMesh.AllAreas;

		if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSampleRadius, areaMask))
		{
			return false;
		}

		reachablePoint = hit.position;
		float edgeClearance = navMeshEdgeClearance;
		if (navMeshAgent != null)
		{
			edgeClearance = Mathf.Max(edgeClearance, navMeshAgent.radius);
		}

		if (edgeClearance > 0f
			&& NavMesh.FindClosestEdge(reachablePoint, out NavMeshHit edgeHit, areaMask)
			&& edgeHit.distance < edgeClearance)
		{
			return false;
		}

		if (navMeshAgent == null || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
		{
			return true;
		}

		float closeEnoughSqr = radialDestinationReachedDistance * radialDestinationReachedDistance;
		if ((reachablePoint - agent.transform.position).sqrMagnitude <= closeEnoughSqr)
		{
			return true;
		}

		if (!NavMesh.CalculatePath(agent.transform.position, reachablePoint, areaMask, SharedNavMeshPath))
		{
			return false;
		}

		return SharedNavMeshPath.status == NavMeshPathStatus.PathComplete;
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

		if (perception.TargetTransform != null)
		{
			if ((perception.TargetPosition - runtimeState.LastTargetPosition).sqrMagnitude > TargetMoveThresholdSqr)
			{
				return false;
			}
		}

		return true;
	}

	private PositionTarget CachePosition(AgentRuntimeState runtimeState, int agentId, Vector3 position, Vector3 agentPosition, Transform targetTransform)
	{
		runtimeState.CachedPosition = position;
		runtimeState.HasCachedPosition = true;
		runtimeState.LastAgentPosition = agentPosition;
		runtimeState.LastTargetTransform = targetTransform;
		if (targetTransform != null)
		{
			runtimeState.LastTargetPosition = targetTransform.position;
		}
		runtimeState.NextSenseTime = Time.time + SenseIntervalSeconds + (Mathf.Abs(agentId) % 5) * 0.01f;
		return new PositionTarget(position);
	}

	bool HasLineOfSight(Vector3 start, Vector3 end)
	{
		return SharedAgentPerception.HasLineOfSight(start, end, AttackConfig);
	}

	public void OnDrawGizmosSelected()
	{
		if (!drawTacticalQueryGizmos)
		{
			return;
		}

		Transform target = LastTacticalDebugTarget;
		if (target == null && !string.IsNullOrEmpty(tacticalDebugTargetName))
		{
			GameObject targetObject = GameObject.Find(tacticalDebugTargetName);
			if (targetObject != null)
			{
				target = targetObject.transform;
			}
		}

		Transform agent = LastTacticalDebugAgent;
		if (agent == null && !string.IsNullOrEmpty(tacticalDebugAgentName))
		{
			GameObject agentObject = GameObject.Find(tacticalDebugAgentName);
			if (agentObject != null)
			{
				agent = agentObject.transform;
			}
		}

		if (target == null)
		{
			return;
		}

		Vector3 targetPosition = LastTacticalDebugTarget == target ? LastTacticalDebugTargetPosition : target.position;
		if (LastTacticalDebugDesiredSector >= 0)
		{
			Gizmos.color = new Color(1f, 0.55f, 0.15f, 1f);
			DrawDebugRing(targetPosition, LastTacticalDebugMinDistance);
			Gizmos.color = new Color(1f, 0.2f, 0.15f, 1f);
			DrawDebugRing(targetPosition, LastTacticalDebugMaxDistance);
			Gizmos.color = new Color(1f, 0.75f, 0.2f, 1f);
			DrawSectorBoundary(targetPosition, LastTacticalDebugDesiredSector, -0.5f, LastTacticalDebugMaxDistance);
			DrawSectorBoundary(targetPosition, LastTacticalDebugDesiredSector, 0.5f, LastTacticalDebugMaxDistance);
		}

		for (int i = 0; i < TacticalCandidateDebugSnapshots.Count; i++)
		{
			TacticalCandidateDebugSnapshot snapshot = TacticalCandidateDebugSnapshots[i];
			Gizmos.color = GetTacticalCandidateGizmoColor(snapshot);
			float radius = snapshot.IsSelected ? 0.4f : 0.22f;
			Gizmos.DrawSphere(snapshot.Position, radius);
			Gizmos.DrawLine(snapshot.Position, targetPosition);
		}

		if (HasLastTacticalDebugBestPoint)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(LastTacticalDebugBestPoint, 0.55f);
		}

		if (agent != null)
		{
			Gizmos.color = Color.white;
			Gizmos.DrawLine(agent.position, targetPosition);
		}
	}

	private void DrawDebugRing(Vector3 center, float radius)
	{
		const int segmentCount = 32;
		Vector3 previousPoint = center + Vector3.right * radius;

		for (int i = 1; i <= segmentCount; i++)
		{
			float angle = i / (float)segmentCount * Mathf.PI * 2f;
			Vector3 nextPoint = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
			Gizmos.DrawLine(previousPoint, nextPoint);
			previousPoint = nextPoint;
		}
	}

	private void DrawSectorBoundary(Vector3 center, int sectorIndex, float edgeOffset, float radius)
	{
		Vector3 direction = GetSectorDirection(sectorIndex, edgeOffset);
		Gizmos.DrawLine(center, center + direction * radius);
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
