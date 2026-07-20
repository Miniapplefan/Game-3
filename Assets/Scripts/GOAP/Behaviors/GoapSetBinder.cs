using CrashKonijn.Goap.Behaviours;
using UnityEngine;

public class GoapSetBinder : MonoBehaviour
{
  private static GoapRunnerBehaviour cachedGoapRunner;

  [SerializeField] public GoapRunnerBehaviour GoapRunner;

  private void Awake()
  {
    if (GoapRunner == null)
    {
      if (cachedGoapRunner == null)
      {
        cachedGoapRunner = FindObjectOfType<GoapRunnerBehaviour>();
      }

      GoapRunner = cachedGoapRunner;
    }
    else
    {
      cachedGoapRunner = GoapRunner;
    }

    AgentBehaviour agent = GetComponent<AgentBehaviour>();
    if (agent == null)
    {
      Debug.LogError($"{nameof(GoapSetBinder)} requires an {nameof(AgentBehaviour)} on the same GameObject.", this);
      enabled = false;
      return;
    }

    if (GoapRunner == null)
    {
      Debug.LogError($"{nameof(GoapSetBinder)} could not find a {nameof(GoapRunnerBehaviour)} in the scene.", this);
      enabled = false;
      return;
    }

    agent.GoapSet = GoapRunner.GetGoapSet("NPCSet");
  }
}
