using CrashKonijn.Goap.Classes;
using CrashKonijn.Goap.Interfaces;
using CrashKonijn.Goap.Sensors;
using Unity.Mathematics;
using UnityEngine;
using Random = UnityEngine.Random;
using UnityEngine.AI;

public class OverheatedSensor : LocalWorldSensorBase
{
  public override void Created()
  {
  }


  public override void Update()
  {
  }

  public override SenseValue Sense(IMonoAgent agent, IComponentReference references)
  {
    return new SenseValue(Mathf.CeilToInt((references.GetCachedComponent<NPCBrain>().bodyState.HeatContainer_getCurrentHeat() - references.GetCachedComponent<NPCBrain>().bodyState.heatContainer.GetAirTemperature()) / (references.GetCachedComponent<NPCBrain>().bodyState.cooling.GetMaxHeat() - references.GetCachedComponent<NPCBrain>().bodyState.heatContainer.GetAirTemperature()) > 0.1f ? 1 : 0));
  }
}