using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(HeatContainer))]
public class Flammable : MonoBehaviour
{
    public HeatContainer heatContainer;
    HeatMaterialScriptableObject heatMat;
    public bool isOnFire = false;
    [SerializeField]
    float fuel;
    public float volume;
    bool burntOut = false;

    float burnTemperature;

    GameObject flameZone;
    public List<HeatContainer> targetsForFlaming;
    public NavMeshSurface navMeshSurface;
    public NavMeshModifier navMeshModifier;
    private int walkableAreaID;
    private int heatDangerAreaID;

    MeshRenderer meshRenderer;

    public Material onFireMaterial;
    private Material defaultMaterial;

    // Start is called before the first frame update
    void Start()
    {
        heatContainer = GetComponent<HeatContainer>();
        heatMat = heatContainer.heatMat;
        burnTemperature = heatMat.ignitionTemperature * heatMat.burnTemperatureMultiplier;
        fuel = heatContainer.GetColliderVolume(GetComponent<Collider>());
        meshRenderer = GetComponent<MeshRenderer>();
        defaultMaterial = meshRenderer.material;
        CreateFlameZone();
        flameZone.SetActive(false);
        // Debug.Log(GetComponent<Collider>().GetType());
        // Debug.Log(typeof(BoxCollider));
        if (navMeshSurface == null)
        {
            navMeshSurface = FindObjectOfType<NavMeshSurface>();
        }
        if (navMeshModifier == null)
        {
            navMeshModifier = GetComponent<NavMeshModifier>();
        }
        walkableAreaID = UnityEngine.AI.NavMesh.GetAreaFromName("Walkable");
        heatDangerAreaID = UnityEngine.AI.NavMesh.GetAreaFromName("HeatDanger");
    }

    void CreateFlameZone()
    {
        var flameZoneObject = new GameObject("FlameZone");
        flameZoneObject.transform.SetParent(this.transform);
        flameZoneObject.transform.localPosition = Vector3.zero;
        var collider = gameObject.GetComponent<Collider>();
        switch (collider.GetType().ToString())
        {
            case "UnityEngine.CapsuleCollider":
                flameZoneObject.AddComponent<CapsuleCollider>();
                var capsuleCollider = flameZoneObject.GetComponent<CapsuleCollider>();
                capsuleCollider.radius *= 1.25f;
                capsuleCollider.height *= 1.25f;
                HandleNoRigidbodiesSphere(capsuleCollider.radius);
                volume = 2 * (4f / 3f) * Mathf.PI * Mathf.Pow(capsuleCollider.radius, 3) + Mathf.PI * Mathf.Pow(capsuleCollider.radius, 2) * capsuleCollider.height;
                break;
            case "UnityEngine.BoxCollider":
                flameZoneObject.AddComponent<BoxCollider>();
                var boxCollider = flameZoneObject.GetComponent<BoxCollider>();
                boxCollider.size *= 1.5f;
                HandleNoRigidbodiesBox(boxCollider.size);
                volume = boxCollider.size.x * boxCollider.size.y * boxCollider.size.z;
                break;
            case "UnityEngine.SphereCollider":
                flameZoneObject.AddComponent<SphereCollider>();
                var sphereCollider = GetComponent<SphereCollider>();
                sphereCollider.radius *= 1.25f;
                HandleNoRigidbodiesSphere(flameZoneObject.GetComponent<SphereCollider>().radius);
                volume = (4 / 3) * Mathf.PI * Mathf.Pow(sphereCollider.radius, 3);
                break;
            default:
                flameZoneObject.AddComponent<SphereCollider>();
                sphereCollider = GetComponent<SphereCollider>();
                sphereCollider.radius *= 1.25f;
                HandleNoRigidbodiesSphere(flameZoneObject.GetComponent<SphereCollider>().radius);
                volume = (4 / 3) * Mathf.PI * Mathf.Pow(sphereCollider.radius, 3);
                break;
        }
        flameZoneObject.GetComponent<Collider>().isTrigger = true;
        flameZoneObject.AddComponent<FlameZone>();
        flameZoneObject.SetActive(true);
        flameZone = flameZoneObject;
    }
    public void HandleFlameZoneEnter(HeatContainer other)
    {
        //targetsForFlaming.Add(other);
        //other.IncreaseHeat(this, heatContainer.currentTemperature * heatContainer.mass);
        targetsForFlaming.Add(other);
        other.isBeingFlamed = true;
    }

    public void HandleFlameZoneStay(HeatContainer other)
    {
        //other.IncreaseHeat(this, heatContainer.currentTemperature * heatContainer.mass);
        //Debug.Log("Flaming " + other.gameObject.name);
    }

    public void HandleFlameZoneExit(HeatContainer other)
    {
        targetsForFlaming.Remove(other);
        other.isBeingFlamed = false;
        //targetsForFlaming.Remove(other);
    }

    void HandleNoRigidbodiesSphere(float radius)
    {
        if (GetComponent<Rigidbody>() != null) return;

        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);

        foreach (var col in colliders)
        {
            if (col.TryGetComponent(out HeatContainer target) && !col.TryGetComponent(out Rigidbody rb))
            {
                targetsForFlaming.Add(target);
                target.isBeingFlamed = true;
            }
        }
    }

    void HandleNoRigidbodiesBox(Vector3 box)
    {
        if (GetComponent<Rigidbody>() != null) return;

        Collider[] colliders = Physics.OverlapBox(transform.position, box);

        foreach (var col in colliders)
        {
            if (col.TryGetComponent(out HeatContainer target) && !col.TryGetComponent(out Rigidbody rb))
            {
                targetsForFlaming.Add(target);
                target.isBeingFlamed = true;
            }
        }
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        if (burntOut)
        {
            return;
        }
        if (fuel <= 0)
        {
            BurntOut();
        }
        else if (isOnFire && heatContainer.GetTemperature() < heatMat.ignitionTemperature * 0.8f)
        {
            Extinguish();
        }
        else if (heatContainer.GetTemperature() > heatMat.ignitionTemperature && !isOnFire)
        {
            setOnFire(true);
        }
        else if (isOnFire)
        {
            Burn();
            foreach (HeatContainer target in targetsForFlaming)
            {
                if (target == heatContainer) continue;
                target.IncreaseHeat(this, heatContainer.currentTemperature * heatContainer.mass);
            }

        }
    }

    void Burn()
    {
        // Consume fuel first
        fuel -= heatMat.fuelBurnRate * Time.deltaTime;
        if (fuel <= 0f) { BurntOut(); return; }

        // How far are we from the desired burn temperature?
        float deficit = burnTemperature - heatContainer.currentTemperature;
        if (deficit <= 0f) return;                    // Already at / above burn temp

        /* ----------------------------------------------------------------
           1.  Compute a dynamic heat‑output multiplier based on deficit.
               More deficit  -> stronger boost
               Near target   -> gentle boost
        -----------------------------------------------------------------*/
        float deficitFrac = 1 / (deficit / burnTemperature);          // 0‑1+
        float boostFactor = Mathf.Clamp(deficitFrac, 0f, 10f);   // soft‑cap at 1

        /* ----------------------------------------------------------------
           2.  Base power: enough energy per second to raise this object
               by 1 °C if no losses occurred.
               E = m * c  (Joules/°C)
        -----------------------------------------------------------------*/
        float basePower = heatContainer.mass * heatMat.specificHeatCapacity; // J per °C

        /* ----------------------------------------------------------------
           3.  Determine how much heat we lose to the air and compensate for that
        -----------------------------------------------------------------*/

        float temperatureDifference = heatContainer.currentTemperature - heatContainer.ambientTemperature;
        float heatTransfer = heatContainer.GetCoolingConstant(heatContainer.heatMat) * Mathf.Abs(temperatureDifference);
        float tempChange = heatTransfer / (heatContainer.mass * heatContainer.specificHeatCapacity) * 6;

        float powerPerSecond = basePower * tempChange * boostFactor;

        // Energy added this frame
        float energy = powerPerSecond * Time.deltaTime;
        heatContainer.IncreaseHeat(this, energy);
    }

    void setOnFire(bool val)
    {
        if (val)
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, 0.5f, 1 << 11);
            if (colliders.Length > 0)
            {
                //Debug.Log("I tried to set on fire but was being cooled by " + colliders[0].gameObject.name + colliders.Length);
                heatContainer.currentTemperature = heatMat.ignitionTemperature * 0.8f;
                return;
            }
            heatContainer.shouldApplyRadiativeHeating = true;
            meshRenderer.material = onFireMaterial;
            flameZone.SetActive(true);
            isOnFire = true;
            navMeshModifier.area = heatDangerAreaID;
            // navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
        }
        else
        {
            heatContainer.shouldApplyRadiativeHeating = false;
            //if (heatContainer.heatBubble != null) heatContainer.heatBubble.RemoveContributer(heatContainer, true);
            flameZone.SetActive(false);
            meshRenderer.material = defaultMaterial;
            navMeshModifier.area = walkableAreaID;
            // navMeshSurface.UpdateNavMesh(navMeshSurface.navMeshData);
            isOnFire = false;
        }
    }

    public void Extinguish()
    {
        setOnFire(false);
        heatContainer.currentTemperature = 21;
    }

    void BurntOut()
    {
        setOnFire(false);
        burntOut = true;
        meshRenderer.material = defaultMaterial;
        meshRenderer.material.SetColor("_Color", Color.black);
    }
}
