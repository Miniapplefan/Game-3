using CrashKonijn.Goap.Behaviours;
using CrashKonijn.Goap.Interfaces;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class AgentLookBehavior : MonoBehaviour
{
  private AIController AIController;
  private NavMeshAgent NavMeshAgent;
  private AgentBehaviour AgentBehaviour;
  private ITarget CurrentTarget;
  [SerializeField] private float MinMoveDistance = 0.25f;
  private Vector3 EyeLevel = new Vector3(0, 2.32f, 0);
  private Vector3 LastPosition;

  private void Awake()
  {
    AgentBehaviour = GetComponent<AgentBehaviour>();
    AIController = GetComponentInChildren<AIController>();
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
    AIController.SetAimTarget(target.Position + EyeLevel);
  }

  // private void EventsOnTargetInRange(ITarget target)
  // {
  //   CurrentTarget = target;
  // }

  private void Update()
  {
    if (CurrentTarget == null)
    {
      return;
    }

    if (MinMoveDistance <= Vector3.Distance(CurrentTarget.Position, LastPosition))
    {
      LastPosition = CurrentTarget.Position;
      AIController.SetAimTarget(CurrentTarget.Position + EyeLevel);
    }
  }

}