using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class BodyInfo : ScriptableObject
{
    public enum systemID
    {
        Legs,
        Sensors,
        Shields,
        Weapons,
        Cooling,
        Head,
        Siphon,
    };

    public systemID[] rawSystems;
    public int[] rawSystemStartLevels;
    public List<Tuple<systemID, int, int>> systemInfo;

}
