using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Interfaces;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentMoveBehavior : MonoBehaviour
{
	private BodyController bodyController;
	private BodyState bodyState;
	private AIController AIController;
	private NavMeshAgent NavMeshAgent;
	public NavMeshSurface navMeshSurface;
	private AgentBehaviour AgentBehaviour;
	private ITarget CurrentTarget;
	[SerializeField] private float MinMoveDistance = 0.25f;
	[SerializeField] private float ArrivalDistanceTolerance = 0.15f;
	[SerializeField] private float ArrivalStallDuration = 0.2f;
	[SerializeField] private float ArrivalVelocityThreshold = 0.05f;
	[SerializeField] private float ArrivalProgressEpsilon = 0.02f;
	[SerializeField] private bool drawPathDebug = false;
	private Vector3 EyeLevel = new Vector3(0, 2.33f, 0);
	private Vector3 LastPosition;
	private float ArrivalStallTimer;
	private float LastRemainingDistance = float.PositiveInfinity;

	private void Awake()
	{
		NavMeshAgent = GetComponent<NavMeshAgent>();
		AgentBehaviour = GetComponent<AgentBehaviour>();
		AIController = GetComponentInChildren<AIController>();
		bodyController = GetComponentInChildren<BodyController>();
		bodyState = GetComponentInChildren<BodyState>();
		navMeshSurface = FindObjectOfType<NavMeshSurface>();
		NavMeshAgent.autoRepath = true;
	}

	private void OnEnable()
	{
		//AgentBehaviour.Events.OnTargetInRange += EventsOnTargetInRange;
		AgentBehaviour.Events.OnTargetChanged += EventsOnTargetChanged;
		AgentBehaviour.Events.OnTargetOutOfRange += EventsOnTargetOutOfRange;
	}

	private void OnDisable()
	{
		// AgentBehaviour.Events.OnTargetInRange -= EventsOnTargetInRange;
		AgentBehaviour.Events.OnTargetChanged -= EventsOnTargetChanged;
		AgentBehaviour.Events.OnTargetOutOfRange -= EventsOnTargetOutOfRange;
	}

	private void EventsOnTargetOutOfRange(ITarget target) { }

	private void EventsOnTargetChanged(ITarget target, bool inRange)
	{
		if (target == null)
		{
			return;
		}

		CurrentTarget = target;
		LastPosition = CurrentTarget.Position;
		ArrivalStallTimer = 0f;
		LastRemainingDistance = float.PositiveInfinity;
		if (NavMeshAgent.enabled)
		{
			NavMeshAgent.ResetPath();
			NavMeshAgent.SetDestination(target.Position);
		}
		NavMeshAgent.updatePosition = true;
		//AIController.SetAimTarget(target.Position + EyeLevel);
	}

	// private void EventsOnTargetInRange(ITarget target)
	// {
	//   CurrentTarget = target;
	// }

	private void Update()
	{
		if (bodyState.isDead) { NavMeshAgent.speed = 0; return; }
		//NavMeshAgent.acceleration = bodyController.legs.getMoveSpeed() * (bodyController.legs.moveAcceleration / 5) * Time.deltaTime;
		//NavMeshAgent.speed = 3.5f * bodyController.legs.getMoveSpeed();

		NavMeshAgent.speed = 3.5f
			* bodyController.legs.getMoveSpeed()
			* BulletTimeManager.GetScale(BulletTimeChannel.EnemyMovement);

		// Vector3 vel = NavMeshAgent.velocity * (bodyController.legs.getMoveSpeed() / 5);

		// NavMeshAgent.velocity.Set(vel.x, vel.y, vel.z);
		// Debug.Log(NavMeshAgent.velocity);

		if (CurrentTarget == null)
		{
			ArrivalStallTimer = 0f;
			LastRemainingDistance = float.PositiveInfinity;
			return;
		}

		if (NavMeshAgent.enabled && HasEffectivelyArrived())
		{
			if (NavMeshAgent.hasPath || NavMeshAgent.desiredVelocity.sqrMagnitude > 0.0001f)
			{
				NavMeshAgent.ResetPath();
			}
			ArrivalStallTimer = 0f;
			LastRemainingDistance = float.PositiveInfinity;
		}

		bodyState.positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
		bodyState.positionTracker.transform.position = NavMeshAgent.destination;
		bodyState.positionTracker2.gameObject.GetComponent<MeshRenderer>().material.color = Color.green;
		bodyState.positionTracker2.transform.position = CurrentTarget.Position;

		if (MinMoveDistance <= Vector3.Distance(CurrentTarget.Position, LastPosition) && NavMeshAgent.enabled)
		{
			LastPosition = CurrentTarget.Position;
			NavMeshAgent.SetDestination(CurrentTarget.Position);
			ArrivalStallTimer = 0f;
			LastRemainingDistance = float.PositiveInfinity;
			//AIController.SetAimTarget(CurrentTarget.Position + EyeLevel);
		}
	}

	public void RefreshCurrentDestination()
	{
		if (CurrentTarget == null || !NavMeshAgent.enabled)
		{
			return;
		}

		LastPosition = CurrentTarget.Position;
		ArrivalStallTimer = 0f;
		LastRemainingDistance = float.PositiveInfinity;
		NavMeshAgent.ResetPath();
		NavMeshAgent.SetDestination(CurrentTarget.Position);
	}

	private bool HasEffectivelyArrived()
	{
		if (CurrentTarget == null || !NavMeshAgent.enabled)
		{
			return false;
		}

		if (NavMeshAgent.pathPending)
		{
			ArrivalStallTimer = 0f;
			LastRemainingDistance = float.PositiveInfinity;
			return false;
		}

		float directDistance = Vector3.Distance(transform.position, CurrentTarget.Position);
		float distanceTolerance = Mathf.Max(MinMoveDistance, NavMeshAgent.stoppingDistance + ArrivalDistanceTolerance);
		float remainingDistance = NavMeshAgent.hasPath ? NavMeshAgent.remainingDistance : directDistance;
		bool hasFiniteRemainingDistance = !float.IsInfinity(remainingDistance) && !float.IsNaN(remainingDistance);

		if (NavMeshAgent.hasPath
			&& hasFiniteRemainingDistance
			&& remainingDistance <= NavMeshAgent.stoppingDistance + ArrivalDistanceTolerance)
		{
			LastRemainingDistance = remainingDistance;
			ArrivalStallTimer = 0f;
			return true;
		}

		bool closeEnough = directDistance <= distanceTolerance;
		float velocityThresholdSqr = ArrivalVelocityThreshold * ArrivalVelocityThreshold;
		bool nearlyStill = NavMeshAgent.velocity.sqrMagnitude <= velocityThresholdSqr;
		bool noProgress = hasFiniteRemainingDistance
			&& !float.IsInfinity(LastRemainingDistance)
			&& LastRemainingDistance - remainingDistance <= ArrivalProgressEpsilon;

		if (closeEnough && nearlyStill && (!NavMeshAgent.hasPath || noProgress))
		{
			ArrivalStallTimer += Time.deltaTime;
			LastRemainingDistance = remainingDistance;
			return ArrivalStallTimer >= ArrivalStallDuration;
		}

		ArrivalStallTimer = 0f;
		LastRemainingDistance = remainingDistance;
		return false;
	}

	private void OnDrawGizmosSelected()
	{
		if (!drawPathDebug)
		{
			return;
		}

		NavMeshAgent agent = NavMeshAgent != null ? NavMeshAgent : GetComponent<NavMeshAgent>();
		if (agent == null || !agent.hasPath)
		{
			return;
		}

		Gizmos.color = agent.pathStatus == NavMeshPathStatus.PathComplete
			? Color.cyan
			: Color.yellow;

		Vector3[] corners = agent.path.corners;
		for (int i = 0; i < corners.Length - 1; i++)
		{
			Gizmos.DrawLine(corners[i], corners[i + 1]);
			Gizmos.DrawSphere(corners[i], 0.15f);
		}

		if (corners.Length > 0)
		{
			Gizmos.DrawSphere(corners[corners.Length - 1], 0.2f);
		}

		Gizmos.color = Color.magenta;
		Gizmos.DrawSphere(agent.steeringTarget, 0.25f);
	}

}
