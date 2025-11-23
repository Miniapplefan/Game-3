using System.Collections;
using UnityEngine;
//using UnityEngine.Pool;
using Lean.Pool;

[CreateAssetMenu(fileName = "Gun", menuName = "Guns/Gun", order = 0)]
public class GunDataScriptableObject : ScriptableObject
{
    //public ImpactType ImpactType;
    public GunType type;
    public string GunName;
    public GameObject ModelPrefab;
    public Vector3 SpawnPoint;
    public Vector3 SpawnRotation;
    public ShootConfigScriptableObject shootConfig;
    public TrailConfigScriptableObject trailConfig;
}
