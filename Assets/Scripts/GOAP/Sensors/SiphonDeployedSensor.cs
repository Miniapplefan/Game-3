using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;

public class SiphonDeployedSensor : LocalWorldSensorBase
{
  public override void Created()
  {
  }

  public override void Update()
  {
  }

  public override SenseValue Sense(IMonoAgent agent, IComponentReference references)
  {
    return new SenseValue(references.GetCachedComponent<NPCBrain>().bodyState.Siphon_isExtended() ? 1 : 0);
  }
}