using System.Collections.Generic;
using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class DefendSiphonTargetSensor : LocalTargetSensorBase, IInjectable
{

	private AttackConfigSO AttackConfig;

	public float circleRadius = 5f;
	public int numberOfPoints = 36;
	private Collider[] Colliders = new Collider[1];

	public override void Created()
	{
	}

	public override void Update()
	{
	}

	public override ITarget Sense(IMonoAgent agent, IComponentReference references)
	{
		if (agent.GetComponentInChildren<BodyState>().legs.getMoveSpeed() <= 0)
		{
			return new PositionTarget(agent.transform.position);
		}

		if (Physics.OverlapSphereNonAlloc(agent.transform.position, AttackConfig.SensorRadius, Colliders, AttackConfig.AttackableLayerMask) > 0 && agent.GetComponentInChildren<BodyState>().siphonTarget != null)
		{
			bool seeTarget = false;
			float distanceToPlayer = Vector3.Distance(agent.transform.position, Colliders[0].transform.position);
			float inRangeDistance = agent.GetComponentInChildren<BodyState>().desiredGunToUse == null ? 10 : agent.GetComponentInChildren<BodyState>().desiredGunToUse.gunData.shootConfig.maxRange;
			float targetDistanceToSiphonMultiplier = Vector3.Distance(Colliders[0].transform.position, agent.GetComponentInChildren<BodyState>().siphonTarget.transform.position) < agent.GetComponentInChildren<BodyState>().siphon.getMaxSiphonDistance() ? 0.3f : 1f;

			// Check if we can see the player
			RaycastHit hit1;
			Vector3 direction1 = (Colliders[0].transform.position - agent.GetComponentInChildren<BodyState>().headCollider.transform.position).normalized;
			if (Physics.SphereCast(agent.GetComponentInChildren<BodyState>().headCollider.transform.position, AttackConfig.LineOfSightSphereCastRadius, direction1, out hit1, Mathf.Infinity, AttackConfig.AttackableLayerMask | AttackConfig.ObstructionLayerMask))
			{
				if (hit1.transform.GetComponent<PlayerController>() != null)
				{
					seeTarget = true;
				}
			}

			if (seeTarget && distanceToPlayer <= inRangeDistance / 1.5f)
			{
				return new PositionTarget(agent.transform.position);
			}
			else if (seeTarget && !(distanceToPlayer <= inRangeDistance / 1.5f))
			{
				int count = 0;

				// Try to find a position within the siphon radius that has line of sight to the player
				while (count < 5)
				{
					Vector3 randomPointOnCircle = GetRandomPointInCircle(agent.GetComponentInChildren<BodyState>().siphonTarget.transform.position, agent.GetComponentInChildren<BodyState>().siphon.getMaxSiphonDistance());
					float distance = Vector3.Distance(agent.transform.position, randomPointOnCircle);

					if (distance < distanceToPlayer && HasLineOfSight(randomPointOnCircle, Colliders[0].transform.position))
					{
						return new PositionTarget(randomPointOnCircle);
					}
					else
					{
						count++;
					}
				}
			}
			else if (!seeTarget && distanceToPlayer <= inRangeDistance / 2)
			{
				List<Vector3> points = new List<Vector3>();
				float lineLength = 20f; // Length of the strafing line
				int numberOfPoints = 30; // Number of points to evaluate
				Vector3 direction = Vector3.Cross(Vector3.up, (Colliders[0].transform.position - agent.transform.position).normalized); // Perpendicular to the player direction

				for (int i = numberOfPoints - 1; i >= 0; i--) // Reverse the order
				{
					float t = (float)i / (numberOfPoints - 1); // Normalize to range [0, 1]
					Vector3 point = agent.transform.position + direction * (t * lineLength - lineLength / 2);
					points.Add(point);
				}

				Vector3 closestPoint = Vector3.zero;
				float closestDistance = float.MaxValue;

				foreach (Vector3 point in points)
				{
					float distanceToAI = Vector3.Distance(point, agent.transform.position);

					if (distanceToAI < distanceToPlayer && HasLineOfSight(point, Colliders[0].transform.position) && Vector3.Distance(point, agent.GetComponentInChildren<BodyState>().siphonTarget.transform.position) <= agent.GetComponentInChildren<BodyState>().siphon.getMaxSiphonDistance() * targetDistanceToSiphonMultiplier)
					{
						if (distanceToAI < closestDistance)
						{
							closestDistance = distanceToAI;
							closestPoint = point;
						}
					}
				}

				if (closestPoint != Vector3.zero)
				{
					return new PositionTarget(closestPoint);
				}
			}
			Vector3 pos;
			// If no valid position found within the siphon radius, pick a random position within the siphon radius
			if (agent.GetComponentInChildren<BodyState>().siphonTarget != null)
			{
				pos = GetRandomPointInCircle(agent.GetComponentInChildren<BodyState>().siphonTarget.transform.position, agent.GetComponentInChildren<BodyState>().siphon.getMaxSiphonDistance() * targetDistanceToSiphonMultiplier);
			}
			else
			{
				pos = agent.transform.position;
			}
			return new PositionTarget(pos);
		}

		Vector3 position;
		// If no target is found, return a random position within the siphon radius
		if (agent.GetComponentInChildren<BodyState>().siphonTarget != null)
		{
			position = GetRandomPointInCircle(agent.GetComponentInChildren<BodyState>().siphonTarget.transform.position, agent.GetComponentInChildren<BodyState>().siphon.getMaxSiphonDistance() * 0.5f);
		}
		else
		{
			position = agent.transform.position;
		}
		return new PositionTarget(position);
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

	private Vector3 GetRandomPointInCircle(Vector3 center, float radius)
	{
		// Random angle between 0 and 2π
		float angle = UnityEngine.Random.Range(0, 2 * Mathf.PI);

		// Random radius between 0 and the given radius
		float randomRadius = UnityEngine.Random.Range(0, radius);

		// Convert polar coordinates (angle and radius) to Cartesian coordinates (x, z)
		float x = center.x + randomRadius * Mathf.Cos(angle);
		float z = center.z + randomRadius * Mathf.Sin(angle);

		// Return the point with the same y-coordinate as the center
		return new Vector3(x, center.y, z);
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

	public void Inject(DependencyInjector injector)
	{
		AttackConfig = injector.AttackConfig;
	}
}