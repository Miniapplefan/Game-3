using UnityEngine;

[CreateAssetMenu(menuName = "AI/Attack Config", fileName = "Attack Config", order = 1)]
public class AttackConfigSO : ScriptableObject
{
	public int SensorRadius = 40;
	public float FOVAngle = 90;
	public float LineOfSightSphereCastRadius = 0.01f;
	public LayerMask AttackableLayerMask;
	public LayerMask AllyLayerMask;

	public LayerMask EnvironmentalCoolingLayerMask;
	public Vector3 EyeLevel = new Vector3(0, 2.33f, 0);

	public LayerMask SiphonableLayerMask;
	[Tooltip("Lower is a better hiding spot")]
	public float HideSensitivity = 0;
	public float MinPlayerDistance = 5f;
	[Range(0, 5f)]
	public float MinObstacleHeight = 1.25f;
	public LayerMask ObstructionLayerMask;
	public float TimeBetweenAttacks = 1;
	public float TimeToAim = 3;
	public float HitStunToUnsetFireReadiness = 0.9f;
	public float SuppressiveShotTimeToAimIncrease = 1f;

	[Header("Combat Shuffle")]
	[Min(0f)] public float DangerCountdownMinSeconds = 3f;
	[Min(0f)] public float DangerCountdownMaxSeconds = 6f;
	[Min(0f)] public float DangerCountdownRecoveryRate = 0.5f;
	[Min(0f)] public float CombatShuffleMinDistance = 1f;
	[Min(0f)] public float CombatShuffleMaxDistance = 2f;
	[Min(0.01f)] public float CombatShuffleNavMeshSampleRadius = 0.35f;
	[Min(0f)] public float CombatShuffleArrivalTolerance = 0.15f;

	public int AttackCost = 4;
	public float SiphonDelay = 1;
}
