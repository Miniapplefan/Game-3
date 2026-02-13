using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;
using CrashKonijn.Goap.Classes.References;
using System.Collections.Generic;

public class CooldownTargetSensor : LocalTargetSensorBase, IInjectable
{
	private AttackConfigSO AttackConfig;
	private Collider[] TargetCollider = new Collider[1];
	private Collider[] Colliders = new Collider[10];
	private Collider[] EnvironmentalCoolingColliders = new Collider[10];
	private Vector3 currentPosition;
	private NavMeshAgent navMeshAgent;
	private readonly List<Vector3> strafePoints = new List<Vector3>(16);

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		currentPosition = agent.transform.position;
		navMeshAgent = agent.GetComponent<NavMeshAgent>();

		Vector3 coverPosition = GetCoverPosition(agent);
		Vector3 environmentalCoolingElementPosition = GetEnvironmentalCoolingPosition(agent);
		Vector3 position;

		//position = 2 * Vector3.Distance(currentPosition, coverPosition) <
		//Vector3.Distance(currentPosition, environmentalCoolingElementPosition) ? coverPosition : environmentalCoolingElementPosition;

		position = environmentalCoolingElementPosition;

		return new PositionTarget(position);
	}

	private Vector3 GetEnvironmentalCoolingPosition(IMonoAgent agent)
	{
		bool targetNearby = false;
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			targetNearby = true;
		}

		Vector3 closestCoolingPostion = Vector3.zero;
		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius * 5, EnvironmentalCoolingColliders, AttackConfig.EnvironmentalCoolingLayerMask) > 0)
		{
			HeatContainer myHeatContainer = agent.GetComponentInChildren<HeatContainer>();
			float closestDistance = Mathf.Infinity;

			for (int i = 0; i < EnvironmentalCoolingColliders.Length; i++)
			{
				Collider environmentalCollider = EnvironmentalCoolingColliders[i];

				// The collider is not active, skip it
				if (environmentalCollider == null || !environmentalCollider.gameObject.activeSelf)
				{
					continue;
				}

				HeatContainer environmentalHeatContainer = environmentalCollider.gameObject.GetComponent<HeatContainer>();
				bool tempCheck = environmentalHeatContainer != null && environmentalHeatContainer.GetTemperature() < myHeatContainer.GetTemperature();

				// The temperature of the collider is hotter than us, skip it
				if (environmentalHeatContainer != null && !tempCheck)
				{
					continue;
				}

				// TODO: Sample random positions within the collider until we find a position that NavMesh.SamplePosition returns true for.
				Vector3 validPosition = Vector3.zero;
				bool foundValidPosition = false;
				int maxAttempts = 30; // Limit the number of sampling attempts
				int attempts = 0;
				var bodyState = agent.GetComponentInChildren<BodyState>();
				var ag = bodyState.heatContainer.airGrid;

				while (!foundValidPosition && attempts < maxAttempts)
				{
					// Generate a random point within the collider's bounds
					Vector3 randomPoint = new Vector3(
							Random.Range(environmentalCollider.bounds.min.x, environmentalCollider.bounds.max.x),
							Random.Range(environmentalCollider.bounds.min.y, environmentalCollider.bounds.max.y),
							Random.Range(environmentalCollider.bounds.min.z, environmentalCollider.bounds.max.z)
					);

					// Check if the random point is inside the collider (bounds are axis-aligned, so we need to verify)
					if (environmentalCollider.ClosestPoint(randomPoint) == randomPoint)
					{
						bool notTooCloseToTarget = true;
						if (targetNearby)
						{
							if (Vector3.Distance(randomPoint, Colliders[0].transform.position) < 6.0f)
							{
								notTooCloseToTarget = false;
							}
						}
						// 	Debug.Log("inside");
						// 	// Check if the point is on the NavMesh
						if (NavMesh.SamplePosition(randomPoint, out NavMeshHit hit, 2.0f, NavMesh.AllAreas) && notTooCloseToTarget)
						{
							//Debug.Log("valid pos");
							validPosition = hit.position;
							foundValidPosition = true;
						}
					}

					attempts++;
				}

				if (!foundValidPosition)
				{
					continue; // Skip this collider if no valid position was found
				}

				// Continue with the distance calculations
				float dist = Vector3.Distance(agent.transform.position, validPosition);

				if (dist < closestDistance)
				{
					closestDistance = dist;
					closestCoolingPostion = validPosition;
				}
			}
		}

		if (closestCoolingPostion != Vector3.zero)
		{
			// Debug.Log("cooling wasn't null");
			// Vector3 closestPointOnCollider = closestCoolingElement.ClosestPoint(agent.transform.position);
			// if (NavMesh.SamplePosition(closestPointOnCollider, out NavMeshHit hit, 1.0f, NavMesh.AllAreas))
			// {
			// Debug.Log("Found cooling position");
			return closestCoolingPostion;
			//}
		}

		//Debug.Log("Falling back to cover position");
		// Debug.Log("No Cool Falling back on cover ");
		return GetCoverPosition(agent);
	}


	private Vector3 GetCoverPosition(IMonoAgent agent)
	{
		int count = 0;

		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0)
		{
			var bodyState = agent.GetComponentInChildren<BodyState>();
			var ag = bodyState.heatContainer.airGrid;
			RaycastHit hit1;
			Vector3 direction1 = (Colliders[0].gameObject.GetComponentInParent<BodyState>().headCollider.transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
			//+ AttackConfig.EyeLevel
			if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
			{
				if (hit1.transform.GetComponent<PlayerController>() != null)
				{
					//********
					// float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);

					// // TODO Huge frame drop spike related to this while loop. spread the work out over multiple frames somehow
					// while (count < 10)
					// {
					// 	Vector3 randomPointOnCircle = GetRandomPointOnCircle(Colliders[0].transform.position, distanceToPlayer);
					// 	float distance = Vector3.Distance(agent.transform.position, randomPointOnCircle);

					// 	if (distance < distanceToPlayer)
					// 	{
					// 		Vector3 direction2 = (Colliders[0].transform.position - randomPointOnCircle).normalized;
					// 		RaycastHit hit2;
					// 		if (Physics.Raycast(randomPointOnCircle, direction2, out hit2, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
					// 		{
					// 			if (hit2.transform.GetComponent<PlayerController>() == null)
					// 			{
					// 				//Debug.Log("Do not see player, moving");
					// 				return randomPointOnCircle;
					// 			}
					// 		}
					// 	}
					// 	count++;
					// }
					//********
					strafePoints.Clear();
					float lineLength = 20f; // Length of the strafing line
					int numberOfPoints = 10; // Number of points to evaluate
					Vector3 direction = Vector3.Cross(Vector3.up, (Colliders[0].transform.position - agent.transform.position).normalized); // Perpendicular to the player direction

					for (int i = numberOfPoints - 1; i >= 0; i--) // Reverse the order
					{
						float t = (float)i / (numberOfPoints - 1); // Normalize to range [0, 1]
						Vector3 point = agent.transform.position + direction * (t * lineLength - lineLength / 2);
						strafePoints.Add(point);
					}

					Vector3 closestPoint = Vector3.zero;
					float closestDistance = float.MaxValue;

					foreach (Vector3 point in strafePoints)
					{
						//Debug.Log("checking points");
						float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);
						float distanceToAI = Vector3.Distance(point, agent.transform.position);
						//distanceToAI < distanceToPlayer && 
						if (!HasLineOfSight(point, Colliders[0].transform.position))
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
						//						Debug.Log("Strafe");
						return closestPoint;
					}


					while (count < 10)
					{
						for (int i = 0; i < Colliders.Length; i++)
						{
							Colliders[i] = null;
						}

						int target = Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, TargetCollider, AttackConfig.AttackableLayerMask);

						int hits = Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.ObstructionLayerMask);

						int hitReduction = 0;
						for (int i = 0; i < hits; i++)
						{
							if (Vector3.Distance(Colliders[i].transform.position, TargetCollider[0].transform.position) < AttackConfig.MinPlayerDistance || Colliders[i].bounds.size.y < AttackConfig.MinObstacleHeight)
							{
								Colliders[i] = null;
								hitReduction++;
							}
						}
						hits -= hitReduction;

						System.Array.Sort(Colliders, ColliderArraySortComparer);

						for (int i = 0; i < hits; i++)
						{
							if (NavMesh.SamplePosition(Colliders[i].transform.position, out NavMeshHit hit, 4f, navMeshAgent.areaMask))
							{
								if (!NavMesh.FindClosestEdge(hit.position, out hit, navMeshAgent.areaMask))
								{
									Debug.LogError($"Unable to find edge close to {hit.position}");
								}

								if (Vector3.Dot(hit.normal, (TargetCollider[0].transform.position - hit.position).normalized) < AttackConfig.HideSensitivity)
								{
									//Debug.Log("Llama");
									return hit.position;
								}
								else
								{
									// Since the previous spot wasn't facing "away" enough from the target, we'll try on the other side of the object
									if (NavMesh.SamplePosition(Colliders[i].transform.position - (TargetCollider[0].transform.position - hit.position).normalized * 2, out NavMeshHit hit2, 2f, navMeshAgent.areaMask))
									{
										if (!NavMesh.FindClosestEdge(hit2.position, out hit2, navMeshAgent.areaMask))
										{
											Debug.LogError($"Unable to find edge close to {hit2.position} (second attempt)");
										}

										if (Vector3.Dot(hit2.normal, (TargetCollider[0].transform.position - hit2.position).normalized) < AttackConfig.HideSensitivity)
										{
											//Debug.Log("Llama");
											return hit2.position;
										}
									}
								}
							}
							else
							{
								Debug.LogError($"Unable to find NavMesh near object {Colliders[i].name} at {Colliders[i].transform.position}");
							}
						}
						count++;

					}
				}
			}
			Vector3 randPos = GetRandomPosition(agent);
			while (Vector3.Distance(randPos, agent.transform.position) > Vector3.Distance(Colliders[0].transform.position, agent.transform.position))
			{
				randPos = GetRandomPosition(agent);
			}
			//Debug.Log("Random");
			return randPos;
		}
		//Debug.Log("Random");
		return GetRandomPosition(agent);
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

		while (count < 5)
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

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}
