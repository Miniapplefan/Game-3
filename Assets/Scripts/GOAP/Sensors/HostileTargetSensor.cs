using System.Collections.Generic;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using UnityEngine;
using UnityEngine.AI;

public class HostileTargetSensor : LocalTargetSensorBase, IInjectable
{
	private const float SenseIntervalSeconds = 0.2f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;
	private const int MaxCirclePointSamples = 12;
	private const int MaxFallbackPointSamples = 8;
	private const int MaxStrafePointSamples = 12;
	private const int RadialSectorCount = 9;
	private const int MaxRadialDistanceSamples = 8;
	private const int CirclePointsPerSense = 4;
	private const int FallbackPointsPerSense = 4;
	private const int StrafePointsPerSense = 4;
	private const int MaxRandomCircleAttempts = 6;

	private AttackConfigSO AttackConfig;

	public float circleRadius = 5f;
	public int numberOfPoints = 36;
	public float minAllySeparationAngle = 20f;
	public float allySeparationWeight = 12f;
	public float distancePenaltyWeight = 0.5f;
	public float allySearchRadiusMultiplier = 1.25f;
	public float navMeshSampleRadius = 1f;
	public float agentFallbackRadius = 6f;
	public int agentFallbackPoints = 16;
	public float agentFallbackPlayerWeight = 1f;
	public float radialMinDistanceMultiplier = 0.35f;
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
	public float radialRecoveryMinDistanceMultiplier = 0.2f;
	public float radialRecoveryMaxDistanceMultiplier = 1f;
	private Collider[] AllyColliders = new Collider[32];
	private List<Vector3> AllyPositions = new List<Vector3>(32);
	private List<Transform> AllyRoots = new List<Transform>(32);
	private readonly List<int> ClaimCleanupAgentIds = new List<int>(16);
	private readonly Dictionary<int, AgentRuntimeState> AgentStates = new Dictionary<int, AgentRuntimeState>();

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

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		var perception = SharedAgentPerception.GetSnapshot(agent, references, AttackConfig);
		var runtimeState = GetRuntimeState(agent);
		var bodyState = perception.BodyState;

		if (bodyState == null)
		{
			return new PositionTarget(agent.transform.position);
		}

		if (bodyState.legs.getMoveSpeed() <= 0)
		{
			return CachePosition(runtimeState, agent.transform.GetInstanceID(), agent.transform.position, agent.transform.position, null);
		}

		if (CanReuseCachedResult(agent.transform.position, runtimeState, perception))
		{
			return new PositionTarget(runtimeState.CachedPosition);
		}

		Transform targetTransform = perception.TargetTransform;
		Vector3 result = agent.transform.position;

		if (perception.HasTarget)
		{
			bool seeTarget = perception.CanSeeTarget;
			Vector3 targetPosition = perception.TargetPosition;
			float distanceToPlayer = Vector3.Distance(agent.transform.position, targetPosition);
			float inRangeDistance = bodyState.desiredGunToUse == null ? 10 : bodyState.desiredGunToUse.gunData.shootConfig.maxRange;


			if (seeTarget && distanceToPlayer <= inRangeDistance / 1.5f)
			{
				result = agent.transform.position;
				UpdateAngleClaim(targetTransform, agent.transform.GetInstanceID(), GetSectorIndex(targetPosition, agent.transform.position), result, agent.transform.position, targetPosition, runtimeState);
			}
			else if (seeTarget && !(distanceToPlayer <= inRangeDistance / 1.5f))
			{
				if (TryGetBestRadialPoint(agent, targetTransform, targetPosition, inRangeDistance, runtimeState, out Vector3 bestPoint))
				{
					result = bestPoint;
				}
				else if (TryGetBestPointOnCircle(targetPosition, inRangeDistance / 2f, agent, runtimeState, true, distanceToPlayer, out bestPoint))
				{
					result = bestPoint;
				}
				else
				{
					result = runtimeState.HasCachedPosition ? runtimeState.CachedPosition : agent.transform.position;
				}
			}

			else if (!seeTarget)//&& distanceToPlayer <= inRangeDistance / 2
			{
				if (TryGetBestRadialRecoveryPoint(agent, targetTransform, targetPosition, inRangeDistance, runtimeState, out Vector3 recoveryPoint))
				{
					result = recoveryPoint;
				}
				else if (TryGetClosestStrafePoint(agent, targetPosition, runtimeState, out Vector3 closestPoint))
				{
					result = closestPoint;
				}
				else if (TryGetBestPointOnCircle(targetPosition, distanceToPlayer, agent, runtimeState, false, float.PositiveInfinity, out Vector3 bestFallback))
				{
					result = bestFallback;
				}
				else if (runtimeState.HasCachedPosition)
				{
					result = runtimeState.CachedPosition;
				}
				else
				{
					result = GetRandomPointOnCircle(targetPosition, distanceToPlayer, agent);
				}
			}
		}
		else if (runtimeState.LastTargetTransform != null && TryGetBestPointOnCircle(runtimeState.LastTargetTransform.position, UnityEngine.Random.Range(1f, 5f), agent, runtimeState, false, float.PositiveInfinity, out Vector3 bestAdvance))
		{
			targetTransform = runtimeState.LastTargetTransform;
			result = bestAdvance;
		}
		else
		{
			result = GetRandomPointOnCircle(agent.transform.position, UnityEngine.Random.Range(1f, 5f), agent);
			targetTransform = null;
		}

		return CachePosition(runtimeState, agent.transform.GetInstanceID(), result, agent.transform.position, targetTransform);
	}

	private bool TryGetBestRadialRecoveryPoint(IMonoAgent agent, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out Vector3 bestPoint)
	{
		return TryGetBestRadialPoint(agent, targetTransform, targetPosition, weaponRange, runtimeState, radialRecoveryMinDistanceMultiplier, radialRecoveryMaxDistanceMultiplier, out bestPoint);
	}

	private bool TryGetBestRadialPoint(IMonoAgent agent, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, out Vector3 bestPoint)
	{
		return TryGetBestRadialPoint(agent, targetTransform, targetPosition, weaponRange, runtimeState, radialMinDistanceMultiplier, radialMaxDistanceMultiplier, out bestPoint);
	}

	private bool TryGetBestRadialPoint(IMonoAgent agent, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, float minDistanceMultiplier, float maxDistanceMultiplier, out Vector3 bestPoint)
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
			if (TryRadialSector(agent, targetState, targetTransform, targetPosition, weaponRange, runtimeState, lockedSectorIndex, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
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

		if (TryGetOpenRadialPoint(agent, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
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

		if (!NavMesh.SamplePosition(claim.ClaimedPosition, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
		{
			return false;
		}

		if (!HasLineOfSight(hit.position, targetPosition))
		{
			return false;
		}

		bestPoint = hit.position;
		return true;
	}

	private void RegisterClaimedSectorFailure(AgentRuntimeState runtimeState)
	{
		runtimeState.ConsecutiveClaimedSectorFailures++;
	}

	private void ResetClaimedSectorFailures(AgentRuntimeState runtimeState)
	{
		runtimeState.ConsecutiveClaimedSectorFailures = 0;
	}

	private bool ShouldDelaySectorSwitch(AgentRuntimeState runtimeState)
	{
		if (Time.time < runtimeState.NextAllowedSectorSwitchTime)
		{
			return true;
		}

		return runtimeState.ConsecutiveClaimedSectorFailures < Mathf.Max(1, radialSectorFailureThreshold);
	}

	private bool TryGetOpenRadialPoint(IMonoAgent agent, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int currentSector, int agentId, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		bool searchPositiveDirectionFirst = ShouldSearchPositiveDirectionFirst(agentId);
		int preferredSector = GetPreferredSectorIndex(agentId);
		int exclusionRadius = Mathf.Max(0, radialClaimSectorExclusionRadius);

		if (TryGetOpenRadialPointWithExclusion(agent, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, preferredSector, searchPositiveDirectionFirst, exclusionRadius, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
		{
			return true;
		}

		if (exclusionRadius <= 0)
		{
			return false;
		}

		return TryGetOpenRadialPointWithExclusion(agent, targetState, targetTransform, targetPosition, weaponRange, runtimeState, currentSector, agentId, preferredSector, searchPositiveDirectionFirst, 0, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint);
	}

	private bool TryGetOpenRadialPointWithExclusion(IMonoAgent agent, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int currentSector, int agentId, int preferredSector, bool searchPositiveDirectionFirst, int exclusionRadius, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;

		for (int i = 0; i < RadialSectorCount; i++)
		{
			if (!TryGetBestOpenSectorCandidate(targetState, currentSector, preferredSector, agentId, attemptedMask, searchPositiveDirectionFirst, exclusionRadius, out int candidateSector))
			{
				break;
			}

			if (TryRadialSector(agent, targetState, targetTransform, targetPosition, weaponRange, runtimeState, candidateSector, agentId, minDistanceMultiplier, maxDistanceMultiplier, ref attemptedMask, out bestPoint))
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

	private bool TryRadialSector(IMonoAgent agent, TargetAngleClaimState targetState, Transform targetTransform, Vector3 targetPosition, float weaponRange, AgentRuntimeState runtimeState, int sectorIndex, int agentId, float minDistanceMultiplier, float maxDistanceMultiplier, ref int attemptedMask, out Vector3 bestPoint)
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

		if (!TrySampleRadialPoint(agent, targetPosition, sectorIndex, weaponRange, minDistanceMultiplier, maxDistanceMultiplier, out bestPoint))
		{
			return false;
		}

		UpdateAngleClaim(targetTransform, agentId, sectorIndex, bestPoint, agent.transform.position, targetPosition, runtimeState);
		return true;
	}

	private bool TrySampleRadialPoint(IMonoAgent agent, Vector3 targetPosition, int sectorIndex, float weaponRange, float minDistanceMultiplier, float maxDistanceMultiplier, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		Vector3 direction = GetSectorDirection(sectorIndex);
		if (direction.sqrMagnitude < 0.0001f)
		{
			return false;
		}

		float minDistance = Mathf.Max(1f, weaponRange * minDistanceMultiplier);
		float maxDistance = Mathf.Max(minDistance + 0.1f, weaponRange * maxDistanceMultiplier);
		float distanceStep = Mathf.Max(0.25f, radialDistanceStep);
		int sampleCount = Mathf.Clamp(Mathf.CeilToInt((maxDistance - minDistance) / distanceStep) + 1, 1, MaxRadialDistanceSamples);

		for (int sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
		{
			float t = sampleCount <= 1 ? 0f : (float)sampleIndex / (sampleCount - 1);
			float radialDistance = Mathf.Lerp(minDistance, maxDistance, t);
			Vector3 rawPoint = targetPosition + direction * radialDistance;

			if (!NavMesh.SamplePosition(rawPoint, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
			{
				continue;
			}

			if (!HasLineOfSight(hit.position, targetPosition))
			{
				continue;
			}

			bestPoint = hit.position;
			return true;
		}

		return false;
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
		float sectorSize = 360f / RadialSectorCount;
		float angle = (WrapSectorIndex(sectorIndex) + 0.5f) * sectorSize * Mathf.Deg2Rad;
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

			if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
				continue;

			Vector3 candidate = hit.position;
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

			if (!NavMesh.SamplePosition(raw, out NavMeshHit hit, navMeshSampleRadius, NavMesh.AllAreas))
				continue;

			Vector3 candidate = hit.position;
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
			float distanceToAgent = Vector3.Distance(point, agent.transform.position);

			if (!HasLineOfSight(point, targetPosition))
			{
				continue;
			}

			if (distanceToAgent < closestDistance)
			{
				closestDistance = distanceToAgent;
				closestPoint = point;
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

			if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1, NavMesh.AllAreas))
			{
				return hit.position;
			}

			count++;
		}

		return agent.transform.position;
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

	private List<Vector3> SampleStrafingPointsForGizmos(Transform agent, Transform target)
	{
		List<Vector3> points = new List<Vector3>();

		if (agent == null || target == null)
			return points;

		int num = 30;
		float lineLength = 20f;

		// perpendicular to agent→target direction
		Vector3 dirToTarget = (target.position - agent.position).normalized;
		Vector3 perpendicular = Vector3.Cross(Vector3.up, dirToTarget);

		for (int i = 0; i < num; i++)
		{
			float t = (float)i / (num - 1);
			float offset = t * lineLength - lineLength / 2f;
			Vector3 point = agent.position + perpendicular * offset;
			points.Add(point);
		}

		return points;
	}

	public void OnDrawGizmosSelected()
	{
		// find player only for gizmos (safe in editor; no need for Sensors)
		PlayerController player = GameObject.Find("Body_total new").GetComponentInChildren<PlayerController>();
		if (player == null)
			return;

		Transform agent = GameObject.Find("NPC_total new").transform;
		Transform target = player.transform;

		// sampling method specifically for Gizmos
		List<Vector3> gizmoPoints = SampleStrafingPointsForGizmos(agent, target);

		foreach (var p in gizmoPoints)
		{
			// draw spheres
			Gizmos.color = Color.cyan;
			Gizmos.DrawWireSphere(p, 0.25f);

			// optional: draw line toward target
			Gizmos.color = Color.white;
			Gizmos.DrawLine(p, target.position);
		}

		// draw perpendicular direction for clarity
		Gizmos.color = Color.yellow;
		Gizmos.DrawLine(agent.position, agent.position + (Vector3.Cross(Vector3.up, (target.position - agent.position).normalized) * 5));
	}

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
