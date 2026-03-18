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
	private Collider[] AllyColliders = new Collider[32];
	private List<Vector3> AllyPositions = new List<Vector3>(32);
	private List<Transform> AllyRoots = new List<Transform>(32);
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
			}
			else if (seeTarget && !(distanceToPlayer <= inRangeDistance / 1.5f))
			{
				if (TryGetBestPointOnCircle(targetPosition, inRangeDistance / 2f, agent, runtimeState, true, distanceToPlayer, out Vector3 bestPoint))
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
				if (TryGetClosestStrafePoint(agent, targetPosition, runtimeState, out Vector3 closestPoint))
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
		int startIndex = runtimeState.StrafePointStartIndex;

		for (int i = 0; i < samplesToCheck; i++)
		{
			int index = (startIndex + i) % totalPoints;
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
