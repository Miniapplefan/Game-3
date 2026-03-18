using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;
using System.Collections.Generic;

public class CoverTargetSensor : LocalTargetSensorBase, IInjectable
{
	private const float SenseIntervalSeconds = 0.2f;
	private const float AgentMoveThresholdSqr = 0.25f;
	private const float TargetMoveThresholdSqr = 1f;
	private const int MaxStrafePointSamples = 6;
	private const int StrafePointsPerSense = 3;
	private const int ObstacleChecksPerSense = 3;
	private const int MaxRandomPositionAttempts = 3;
	private const float ObstacleRefreshIntervalSeconds = 0.35f;
	private const float ObstacleRefreshMoveThresholdSqr = 1f;

	private AttackConfigSO AttackConfig;
	private Collider[] EnvironmentalCoolingColliders = new Collider[10];
	private Vector3 currentPosition;
	private NavMeshAgent navMeshAgent;
	private readonly Dictionary<int, AgentRuntimeState> AgentStates = new Dictionary<int, AgentRuntimeState>();

	private sealed class AgentRuntimeState
	{
		public readonly Collider[] CachedObstacles = new Collider[10];
		public Vector3 CachedPosition;
		public bool HasCachedPosition;
		public float NextSenseTime;
		public Vector3 LastAgentPosition;
		public Transform LastTargetTransform;
		public Vector3 LastTargetPosition;
		public int StrafePointStartIndex;
		public int ObstacleScanStartIndex;
		public int CachedObstacleCount;
		public float NextObstacleRefreshTime;
		public Transform ObstacleTargetTransform;
		public Vector3 ObstacleTargetPosition;
		public Vector3 ObstacleAgentPosition;
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
		currentPosition = agent.transform.position;
		navMeshAgent = perception.NavMeshAgent;

		if (perception.BodyState == null || navMeshAgent == null)
		{
			return new PositionTarget(agent.transform.position);
		}

		if (CanReuseCachedResult(agent.transform.position, runtimeState, perception))
		{
			return new PositionTarget(runtimeState.CachedPosition);
		}

		Vector3 position = GetCoverPosition(agent, perception, runtimeState, out Transform targetTransform);
		return CachePosition(runtimeState, agent.transform.GetInstanceID(), position, agent.transform.position, targetTransform);
	}

	private Vector3 GetEnvironmentalCoolingPosition(IMonoAgent agent)
	{
		Collider closestCoolingElement = null;
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, EnvironmentalCoolingColliders, AttackConfig.EnvironmentalCoolingLayerMask) > 0)
		{
			// Assume the AI has a HeatContainer attached to it
			HeatContainer myHeatContainer = agent.GetComponentInChildren<HeatContainer>();
			// Debug.Log("My Heat: " + myHeatContainer.GetTemperature());

			float closestDistance = Mathf.Infinity;

			for (int i = 0; i < EnvironmentalCoolingColliders.Length; i++)
			{
				Collider environmentalCollider = EnvironmentalCoolingColliders[i];
				if (environmentalCollider == null)
				{
					continue;
				}

				// Check if the environmental object has a HeatContainer
				//Debug.Log(environmentalCollider.gameObject.name);
				// Component[] components = environmentalCollider.GetComponents(typeof(Component));
				// foreach (Component component in components)
				// {
				// 	Debug.Log(component.ToString());
				// }

				bool tempCheck = true;
				HeatContainer environmentalHeatContainer = environmentalCollider.gameObject.GetComponent<HeatContainer>();
				if (environmentalHeatContainer != null)
				{
					tempCheck = environmentalHeatContainer.GetTemperature() < myHeatContainer.GetTemperature();
				}

				float dist = Vector3.Distance(agent.transform.position, environmentalCollider.transform.position);
				bool distCheck = dist < closestDistance;

				if (tempCheck && distCheck)
				{
					closestDistance = dist;
					closestCoolingElement = environmentalCollider;
				}
				else
				{
					continue;
				}
			}
		}
		if (closestCoolingElement != null)
		{
			return closestCoolingElement.transform.position;
		}
		else
		{
			return new Vector3(Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
		}
	}

	private Vector3 GetCoverPosition(IMonoAgent agent, SharedAgentPerception.Snapshot perception, AgentRuntimeState runtimeState, out Transform targetTransform)
	{
		targetTransform = perception.TargetTransform;
		if (perception.HasTarget)
		{
			Vector3 targetPosition = perception.TargetPosition;
			if (perception.CanSeeTarget)
			{
				if (TryGetClosestHiddenStrafePoint(agent, targetPosition, runtimeState, out Vector3 closestPoint))
				{
					return closestPoint;
				}

				if (TryGetIncrementalCoverPoint(agent, targetTransform, targetPosition, runtimeState, out Vector3 coverPoint))
				{
					return coverPoint;
				}
			}

			Vector3 randPos = GetRandomPosition(agent);
			while (Vector3.Distance(randPos, agent.transform.position) > Vector3.Distance(targetPosition, agent.transform.position))
			{
				randPos = GetRandomPosition(agent);
			}
			//Debug.Log("Random");
			return randPos;
		}

		if (runtimeState.LastTargetTransform != null)
		{
			targetTransform = runtimeState.LastTargetTransform;
		}

		//Debug.Log("Random");
		return GetRandomPosition(agent);
	}

	private bool TryGetClosestHiddenStrafePoint(IMonoAgent agent, Vector3 targetPosition, AgentRuntimeState runtimeState, out Vector3 closestPoint)
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
			Vector3 point = agent.transform.position + direction * (t * lineLength - lineLength / 2f);
			float distanceToAgent = Vector3.Distance(point, agent.transform.position);

			if (HasLineOfSight(point, targetPosition))
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

	private bool TryGetIncrementalCoverPoint(IMonoAgent agent, Transform targetTransform, Vector3 targetPosition, AgentRuntimeState runtimeState, out Vector3 coverPoint)
	{
		coverPoint = agent.transform.position;
		RefreshObstacleCandidatesIfNeeded(agent, runtimeState, targetTransform, targetPosition);
		if (runtimeState.CachedObstacleCount <= 0)
		{
			return false;
		}

		int checksToRun = Mathf.Min(runtimeState.CachedObstacleCount, ObstacleChecksPerSense);
		int startIndex = runtimeState.ObstacleScanStartIndex;

		for (int i = 0; i < checksToRun; i++)
		{
			int index = (startIndex + i) % runtimeState.CachedObstacleCount;
			Collider obstacle = runtimeState.CachedObstacles[index];
			if (obstacle == null)
			{
				continue;
			}

			if (TryGetCoverPointNearObstacle(obstacle, targetPosition, out coverPoint))
			{
				runtimeState.ObstacleScanStartIndex = (index + 1) % runtimeState.CachedObstacleCount;
				return true;
			}
		}

		runtimeState.ObstacleScanStartIndex = (startIndex + checksToRun) % runtimeState.CachedObstacleCount;
		return false;
	}

	private void RefreshObstacleCandidatesIfNeeded(IMonoAgent agent, AgentRuntimeState runtimeState, Transform targetTransform, Vector3 targetPosition)
	{
		bool shouldRefresh = Time.time >= runtimeState.NextObstacleRefreshTime
			|| runtimeState.ObstacleTargetTransform != targetTransform
			|| (agent.transform.position - runtimeState.ObstacleAgentPosition).sqrMagnitude > ObstacleRefreshMoveThresholdSqr
			|| (targetPosition - runtimeState.ObstacleTargetPosition).sqrMagnitude > ObstacleRefreshMoveThresholdSqr;

		if (!shouldRefresh)
		{
			return;
		}

		for (int i = 0; i < runtimeState.CachedObstacles.Length; i++)
		{
			runtimeState.CachedObstacles[i] = null;
		}

		int hits = Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, runtimeState.CachedObstacles, AttackConfig.ObstructionLayerMask);
		int filteredCount = 0;
		for (int i = 0; i < hits; i++)
		{
			Collider obstacle = runtimeState.CachedObstacles[i];
			if (obstacle == null)
			{
				continue;
			}

			if (Vector3.Distance(obstacle.transform.position, targetPosition) < AttackConfig.MinPlayerDistance || obstacle.bounds.size.y < AttackConfig.MinObstacleHeight)
			{
				continue;
			}

			runtimeState.CachedObstacles[filteredCount] = obstacle;
			filteredCount++;
		}

		for (int i = filteredCount; i < runtimeState.CachedObstacles.Length; i++)
		{
			runtimeState.CachedObstacles[i] = null;
		}

		runtimeState.CachedObstacleCount = filteredCount;
		currentPosition = agent.transform.position;
		System.Array.Sort(runtimeState.CachedObstacles, ColliderArraySortComparer);
		runtimeState.ObstacleScanStartIndex = 0;
		runtimeState.ObstacleTargetTransform = targetTransform;
		runtimeState.ObstacleTargetPosition = targetPosition;
		runtimeState.ObstacleAgentPosition = agent.transform.position;
		runtimeState.NextObstacleRefreshTime = Time.time + ObstacleRefreshIntervalSeconds;
	}

	private bool TryGetCoverPointNearObstacle(Collider obstacle, Vector3 targetPosition, out Vector3 coverPoint)
	{
		coverPoint = obstacle.transform.position;
		if (NavMesh.SamplePosition(obstacle.transform.position, out NavMeshHit hit, 4f, navMeshAgent.areaMask))
		{
			if (!NavMesh.FindClosestEdge(hit.position, out hit, navMeshAgent.areaMask))
			{
				Debug.LogError($"Unable to find edge close to {hit.position}");
			}

			if (Vector3.Dot(hit.normal, (targetPosition - hit.position).normalized) < AttackConfig.HideSensitivity)
			{
				coverPoint = hit.position;
				return true;
			}

			Vector3 fallbackDirection = (targetPosition - hit.position).normalized;
			if (NavMesh.SamplePosition(obstacle.transform.position - fallbackDirection * 2f, out NavMeshHit hit2, 2f, navMeshAgent.areaMask))
			{
				if (!NavMesh.FindClosestEdge(hit2.position, out hit2, navMeshAgent.areaMask))
				{
					Debug.LogError($"Unable to find edge close to {hit2.position} (second attempt)");
				}

				if (Vector3.Dot(hit2.normal, (targetPosition - hit2.position).normalized) < AttackConfig.HideSensitivity)
				{
					coverPoint = hit2.position;
					return true;
				}
			}

			return false;
		}

		Debug.LogError($"Unable to find NavMesh near object {obstacle.name} at {obstacle.transform.position}");
		return false;
	}

	bool HasLineOfSight(Vector3 start, Vector3 end)
	{
		return SharedAgentPerception.HasLineOfSight(start, end, AttackConfig);
	}

	public int ColliderArraySortComparer(Collider A, Collider B)
	{
		if (A == null && B != null)
		{
			return 1;
		}
		else if (A != null && B == null)
		{
			return -1;
		}
		else if (A == null && B == null)
		{
			return 0;
		}
		else
		{
			return Vector3.Distance(currentPosition, A.transform.position).CompareTo(Vector3.Distance(currentPosition, B.transform.position));
		}
	}

	private Vector3 GetRandomPosition(IMonoAgent agent)
	{
		int count = 0;

		while (count < MaxRandomPositionAttempts)
		{
			Vector2 random = Random.insideUnitCircle * 10;
			Vector3 position = agent.transform.position + new UnityEngine.Vector3(
				random.x,
				0,
				random.y
			);

			if (NavMesh.SamplePosition(position, out NavMeshHit hit, 1, NavMesh.AllAreas))
			{
				return hit.position;
			}

			count++;
		}

		return agent.transform.position;
	}

	private Vector3 GetRandomPointOnCircle(Vector3 center, float radius)
	{
		// Generate a random angle between 0 and 2π
		float randomAngle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);

		// Calculate the x and z coordinates of the random point on the circle
		float x = center.x + radius * Mathf.Cos(randomAngle);
		float z = center.z + radius * Mathf.Sin(randomAngle);

		// Set the y coordinate to the center's y coordinate (assuming the circle is on the same plane)
		float y = center.y;

		// Return the random point on the circle
		return new Vector3(x, y, z);
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

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
