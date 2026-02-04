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
	private Vector3 EyeLevel = new Vector3(0, 2.33f, 0);
	private Vector3 LastPosition;

	private void Awake()
	{
		NavMeshAgent = GetComponent<NavMeshAgent>();
		AgentBehaviour = GetComponent<AgentBehaviour>();
		AIController = GetComponentInChildren<AIController>();
		bodyController = GetComponentInChildren<BodyController>();
		bodyState = GetComponentInChildren<BodyState>();
		navMeshSurface = FindObjectOfType<NavMeshSurface>();
		// NavMeshAgent.autoRepath = true;
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
		CurrentTarget = target;
		LastPosition = CurrentTarget.Position;
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

		NavMeshAgent.speed = 3.5f * bodyController.legs.getMoveSpeed();

		// Vector3 vel = NavMeshAgent.velocity * (bodyController.legs.getMoveSpeed() / 5);

		// NavMeshAgent.velocity.Set(vel.x, vel.y, vel.z);
		// Debug.Log(NavMeshAgent.velocity);

		if (CurrentTarget == null)
		{
			return;
		}
		bodyState.positionTracker.gameObject.GetComponent<MeshRenderer>().material.color = Color.white;
		bodyState.positionTracker.transform.position = NavMeshAgent.destination;
		bodyState.positionTracker2.gameObject.GetComponent<MeshRenderer>().material.color = Color.green;
		bodyState.positionTracker2.transform.position = CurrentTarget.Position;

		if (MinMoveDistance <= Vector3.Distance(CurrentTarget.Position, LastPosition) && NavMeshAgent.enabled)
		{
			navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
			navMeshSurface.BuildNavMesh();
			NavMeshAgent.ResetPath();
			LastPosition = CurrentTarget.Position;
			NavMeshAgent.SetDestination(CurrentTarget.Position);
			//AIController.SetAimTarget(CurrentTarget.Position + EyeLevel);
		}
	}

}