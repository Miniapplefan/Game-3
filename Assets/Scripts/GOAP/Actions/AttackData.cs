using CrashKonijn.Goap.Classes.References;
using UnityEngine;
using UnityEngine.AI;

public class AttackData : CommonData
{
  [GetComponentInChildren]
  public AIController AIController { get; private set; }
  [GetComponent]
  public NavMeshAgent navMeshAgent { get; private set; }
  public Vector3 targetPositionToLookAt { get; set; }
  public BodyState targetState { get; set; }
}