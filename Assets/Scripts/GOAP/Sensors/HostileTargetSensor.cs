using System.Collections.Generic;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public class HostileTargetSensor : LocalTargetSensorBase, IInjectable
{

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
	private Collider[] Colliders = new Collider[1];
	private Collider[] AllyColliders = new Collider[32];
	private List<Vector3> AllyPositions = new List<Vector3>(32);
	private List<Transform> AllyRoots = new List<Transform>(32);

	private List<Vector3> DebugPoints = new List<Vector3>();

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{

		var bodyState = agent.GetComponentInChildren<BodyState>();
		var ag = bodyState.heatContainer.airGrid;

		if (bodyState.legs.getMoveSpeed() <= 0)
		{
			return new PositionTarget(agent.transform.position);
		}

		//string s = "";
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			bool seeTarget = false;
			float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);
			float inRangeDistance = bodyState.desiredGunToUse == null ? 10 : bodyState.desiredGunToUse.gunData.shootConfig.maxRange;

			//Player is in range, check if we can see them
			RaycastHit hit1;
			Vector3 direction1 = (Colliders[0].transform.position - bodyState.headCollider.transform.position).normalized;
			//Physics.Raycast(agent.transform.position, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask)
			//Physics.SphereCast(agent.transform.position, 0.2f, direction1, out hit1, Mathf.Infinity,AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask)
			if (Physics.SphereCast(bodyState.headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
			{
				if (hit1.transform.GetComponent<PlayerController>() != null)
				{
					seeTarget = true;
				}
			}


			if (seeTarget && distanceToPlayer <= inRangeDistance / 1.5f)
			{
				//Debug.Log("Can already see player and in range, staying put " + inRangeDistance);
				return new PositionTarget(agent.transform.position);
			}
			else if (seeTarget && !(distanceToPlayer <= inRangeDistance / 1.5f))
			{
				//Debug.Log("Can already see player and NOT in range " + inRangeDistance);
				if (TryGetBestPointOnCircle(Colliders[0].transform.position, inRangeDistance / 2f, agent, true, distanceToPlayer, out Vector3 bestPoint))
				{
					return new PositionTarget(bestPoint);
				}
			}

			else if (!seeTarget)//&& distanceToPlayer <= inRangeDistance / 2
			{
				//Debug.Log("Do not see player and in range " + inRangeDistance);

				List<Vector3> points = new List<Vector3>();
				float lineLength = 20f; // Length of the strafing line
				int numberOfPoints = 30; // Number of points to evaluate
				Vector3 direction = Vector3.Cross(Vector3.up, (Colliders[0].transform.position - agent.transform.position).normalized); // Perpendicular to the player direction
				bool reverse = UnityEngine.Random.value < 0.5f;

				for (int i = 0; i < numberOfPoints; i++)
				{
					int index = reverse ? (numberOfPoints - 1 - i) : i;
					float t = (float)index / (numberOfPoints - 1); // Normalize to range [0, 1]
					Vector3 point = agent.transform.position + new Vector3(0, 2f, 0) + direction * (t * lineLength - lineLength / 2);
					points.Add(point);
				}
				DebugPoints = points;

				Vector3 closestPoint = Vector3.zero;
				float closestDistance = float.MaxValue;

				foreach (Vector3 point in points)
				{
					//Debug.Log("checking points");
					float distanceToAI = Vector3.Distance(point, agent.transform.position);
					// distanceToAI < distanceToPlayer &&
					if (HasLineOfSight(point, Colliders[0].transform.position))
					{
						//Debug.Log("point within range and has los");
						if (distanceToAI < closestDistance)
						{
							//Debug.Log("closer point found");
							closestDistance = distanceToAI;
							closestPoint = point;
						}
					}
				}

				if (closestPoint != Vector3.zero)
				{
					return new PositionTarget(closestPoint);
				}
				//Debug.Log("no point found");
				// return new PositionTarget(agent.transform.position);
				if (TryGetBestPointOnCircle(Colliders[0].transform.position, distanceToPlayer, agent, false, float.PositiveInfinity, out Vector3 bestFallback))
				{
					return new PositionTarget(bestFallback);
				}
				return new PositionTarget(GetRandomPointOnCircle(Colliders[0].transform.position, distanceToPlayer, agent));
			}

			// List<Vector3> points = new List<Vector3>();
			// float angleStep = 360f / numberOfPoints;

			// for (int i = 0; i < numberOfPoints; i++)
			// {
			// 	float angle = i * angleStep;
			// 	Vector3 point = agent.transform.position + new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), 0, Mathf.Sin(angle * Mathf.Deg2Rad)) * circleRadius;
			// 	points.Add(point);
			// }

			// ---------------------------
			// List<Vector3> points = new List<Vector3>();
			// float lineLength = 20f; // Length of the strafing line
			// int numberOfPoints = 30; // Number of points to evaluate
			// Vector3 direction = Vector3.Cross(Vector3.up, (Colliders[0].transform.position - agent.transform.position).normalized); // Perpendicular to the player direction

			// for (int i = numberOfPoints - 1; i >= 0; i--) // Reverse the order
			// {
			// 	float t = (float)i / (numberOfPoints - 1); // Normalize to range [0, 1]
			// 	Vector3 point = agent.transform.position + direction * (t * lineLength - lineLength / 2);
			// 	points.Add(point);
			// }

			// Vector3 closestPoint = Vector3.zero;
			// float closestDistance = float.MaxValue;

			// foreach (Vector3 point in points)
			// {
			// 	//Debug.Log("checking points");
			// 	float distanceToAI = Vector3.Distance(point, agent.transform.position);

			// 	if (distanceToAI < distanceToPlayer && HasLineOfSight(point, Colliders[0].transform.position))
			// 	{
			// 		//Debug.Log("point within range and has los");
			// 		if (distanceToAI < closestDistance)
			// 		{
			// 			//Debug.Log("closer point found");
			// 			closestDistance = distanceToAI;
			// 			closestPoint = point;
			// 		}
			// 	}
			// }

			// if (closestPoint != Vector3.zero)
			// {
			// 	return new PositionTarget(closestPoint);
			// }
			// Debug.Log("no point found");
			// return new PositionTarget(Colliders[0].transform.position);
			//------------------
			//s = "all ifs cleared";
		}

		// if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		// {
		//   Transform target = Colliders[0].transform;
		//   Vector3 directionToTarget = (target.position - agent.transform.position).normalized;

		//   Debug.Log("overlapping");
		//   if (Vector3.Angle(agent.transform.forward, directionToTarget) < AttackConfig.FOVAngle / 2)
		//   {
		//     float distanceToTarget = Vector3.Distance(agent.transform.position, target.position);
		//     if (!Physics.Raycast(agent.transform.position, directionToTarget, distanceToTarget, AttackConfig.ObstructionLayerMask))
		//     {
		//       return new TransformTarget(target);
		//     }
		//     else
		//     {
		//       return null;
		//     }
		//   }
		//   else
		//   {
		//     return null;
		//   }
		// }
		//Debug.Log("Advancing ");
		if (TryGetBestPointOnCircle(Colliders[0].transform.position, UnityEngine.Random.Range(1f, 5f), agent, false, float.PositiveInfinity, out Vector3 bestAdvance))
		{
			return new PositionTarget(bestAdvance);
		}
		return new PositionTarget(GetRandomPointOnCircle(Colliders[0].transform.position, UnityEngine.Random.Range(1f, 5f), agent));
	}

	private bool TryGetBestPointOnCircle(Vector3 center, float radius, IMonoAgent agent, bool requireLineOfSight, float maxDistanceFromAgent, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		if (numberOfPoints <= 0)
			return false;

		float searchRadius = Mathf.Max(radius * allySearchRadiusMultiplier, 1f);
		RefreshAllyPositions(center, agent.transform, searchRadius);

		float angleStep = 360f / numberOfPoints;
		float angleOffset = GetAgentAngleOffset(agent);
		bool reverse = UnityEngine.Random.value < 0.5f;
		float bestScore = float.NegativeInfinity;
		bool found = false;

		for (int i = 0; i < numberOfPoints; i++)
		{
			int index = reverse ? (numberOfPoints - 1 - i) : i;
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

		if (found)
			return true;

		return TryGetBestPointAroundAgent(agent, center, UnityEngine.Random.Range(0.2f, 0.5f), out bestPoint);
	}

	private bool TryGetBestPointAroundAgent(IMonoAgent agent, Vector3 playerPosition, float maxDistanceFromAgent, out Vector3 bestPoint)
	{
		bestPoint = agent.transform.position;
		if (agentFallbackPoints <= 0 || agentFallbackRadius <= 0f)
			return false;

		float angleStep = 360f / agentFallbackPoints;
		float angleOffset = GetAgentAngleOffset(agent);
		bool reverse = UnityEngine.Random.value < 0.5f;
		float bestScore = float.NegativeInfinity;
		bool found = false;

		for (int i = 0; i < agentFallbackPoints; i++)
		{
			int index = reverse ? (agentFallbackPoints - 1 - i) : i;
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

		return found;
	}

	private void RefreshAllyPositions(Vector3 center, Transform self, float searchRadius)
	{
		AllyPositions.Clear();
		AllyRoots.Clear();

		int count = Physics.OverlapSphereNonAlloc(center, searchRadius, AllyColliders, AttackConfig.AllyLayerMask);
		for (int i = 0; i < count; i++)
		{
			var col = AllyColliders[i];
			var colBodyState = col.GetComponent<BodyState>();

			if (col == null)
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

		while (count < 15)
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

	bool HasLineOfSight(Vector3 start, Vector3 end)
	{
		RaycastHit hit;
		//Physics.Raycast(start, (end - start).normalized, out hit, Mathf.Infinity)
		if (Physics.SphereCast(start, AttackConfig.LineOfSightSphereCastRadius, (end - start).normalized, out hit, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
		{
			//Debug.Log(hit.transform.GetComponent<PlayerController>() != null);
			return hit.transform.GetComponent<PlayerController>() != null;
		}
		return false;
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
