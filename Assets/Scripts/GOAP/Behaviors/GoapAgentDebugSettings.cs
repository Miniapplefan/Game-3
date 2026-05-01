using UnityEngine;

[DisallowMultipleComponent]
public class GoapAgentDebugSettings : MonoBehaviour
{
	[Header("Hostile Target Sensor")]
	public bool enableHostileTargetSensorDebug = false;
	public bool showHostileTargetLosMarker = false;
	public bool showHostileTargetTacticalQueryMarkers = false;
	public bool logHostileTargetTacticalQuerySummary = false;
	public bool logHostileTargetSelections = false;
	public bool logHostileTargetFallbackSelections = false;
	public bool logHostileTargetWideFlankSelections = false;
}
