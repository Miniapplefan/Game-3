using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HeatBubble : MonoBehaviour
{
    public List<HeatContainer> contributers;
    public List<HeatContainer> targetsForHeating;
    public Flammable bubbleStarter;
    Vector3 centerOfMass;
    float radius;
    public float averageTemperature;
    [SerializeField]
    float totalMass;
    // private bool resizing = false;
    // private bool resizeQueued = false;
    public SphereCollider bubble;
    public GameObject debugSphere;
    MeshRenderer debugRenderer;

    Transform visualTf;

    // Start is called before the first frame update
    void Start()
    {
        // contributers = new List<HeatContainer>();
        // targetsForHeating = new List<HeatContainer>();
    }

    void Update()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.1f);
        {
            foreach (var col in colliders)
            {
                if (col.TryGetComponent(out HeatBubble target) && target.gameObject != gameObject && target.totalMass > totalMass)
                {
                    foreach (var hc in contributers)
                    {
                        hc.heatBubble = null;
                    }
                    Debug.Log("I destroyed myself in update - someone bigger nearby");
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    void Awake()
    {
        contributers = new List<HeatContainer>();
        targetsForHeating = new List<HeatContainer>();
        bubble = GetComponent<SphereCollider>();

        // debugSphere = new GameObject("heatBubbleDebugSphere");
        // debugSphere.transform.SetParent(this.transform);

        // GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        // Mesh sphereMesh = sphere.GetComponent<MeshFilter>().mesh;
        // debugSphere.AddComponent<MeshFilter>();
        // debugSphere.AddComponent<MeshRenderer>();
        // debugSphere.GetComponent<MeshFilter>().mesh = sphereMesh;
        // Destroy(sphere);
        // ---------- visual shell ----------
        GameObject vis = new GameObject("HeatBubbleVisual");
        vis.transform.SetParent(transform);
        vis.transform.localPosition = Vector3.zero;
        vis.transform.localRotation = Quaternion.identity;

        // Sphere mesh
        MeshFilter mf = vis.AddComponent<MeshFilter>();
        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");  // Unity built‑in sphere

        debugRenderer = vis.AddComponent<MeshRenderer>();
        debugRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        debugRenderer.receiveShadows = false;
        debugRenderer.material = MakeDebugMaterial();

        visualTf = vis.transform;

        // Initial scale
        SyncVisualScale();
    }

    Material MakeDebugMaterial()
    {
        // Use Standard shader (Built‑in RP)
        var mat = new Material(Shader.Find("Standard"));

        // Light yellow, mostly transparent
        Color c = new Color(1f, 1f, 0f, 0.15f);
        mat.SetColor("_Color", c);          // Standard shader tint

        // --- make the Standard shader render transparently ---
        // 0=Opaque 1=Cutout 2=Fade 3=Transparent
        mat.SetFloat("_Mode", 2f);          // Fade (alpha‑blended, keeps specular)
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);           // don’t write to depth
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        // Render after opaque objects
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        // Optional: back‑face culling off so inside faces are visible
        mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        return mat;
    }

    // Call after every Resize() or whenever bubble.radius changes
    void SyncVisualScale()
    {
        float diam = bubble.radius;
        visualTf.localScale = Vector3.one * diam;
    }

    public void AddContributer(HeatContainer contributer, bool shouldResize = false)
    {
        //if (contributers == null) Debug.Log("cont is null"); else Debug.Log("cont is not null");
        if (!contributers.Contains(contributer))
        {
            contributers.Add(contributer);
            contributer.heatBubble = this;
        }
        if (shouldResize)
        {
            Resize();
        }
    }

    public void RemoveContributer(HeatContainer contributer, bool shouldResize)
    {
        contributer.heatBubble = null;
        contributers.Remove(contributer);
        if (shouldResize)
        {
            Resize();
        }
    }


    public void AddTargetForHeating(HeatContainer target)
    {
        if (!targetsForHeating.Contains(target))
        {
            targetsForHeating.Add(target);
        }
    }

    public void RemoveTargetForHeating(HeatContainer target)
    {
        if (targetsForHeating.Contains(target))
        {
            targetsForHeating.Remove(target);
        }
    }

    void Resize()
    {
        // if (resizing)
        // {
        //     resizeQueued = true;
        //     return;
        // }

        // resizing = true;
        // resizeQueued = false;

        Collider[] colliders = Physics.OverlapSphere(transform.position, 0.01f);
        {
            foreach (var col in colliders)
            {
                if (col.TryGetComponent(out HeatBubble target) && target.gameObject != gameObject && target.totalMass > totalMass)
                {
                    foreach (var hc in contributers)
                    {
                        hc.heatBubble = null;
                    }
                    Debug.Log("I destroyed myself - someone bigger nearby");
                    Destroy(gameObject);
                    return;
                }
            }
        }

        // If no contributors left, destroy the bubble
        if (contributers == null || contributers.Count == 0)
        {
            foreach (var hc in contributers)
            {
                hc.heatBubble = null;
            }
            Debug.Log("I destroyed myself - no one left");
            Destroy(gameObject);
            return;
        }

        // Recompute center of mass and average temperature
        Vector3 weightedSum = Vector3.zero;
        float totalTemperature = 0f;
        totalMass = 0;

        foreach (HeatContainer hc in contributers)
        {
            if (hc == null) continue;
            totalMass += hc.mass;
            weightedSum += hc.transform.position * hc.mass;
            float temp = hc.currentTemperature;
            if (float.IsNaN(temp) || float.IsInfinity(temp))
            {
                Debug.LogWarning($"HeatBubble Resize: Invalid temperature {temp} from {hc.name}");
                continue;
            }
            totalTemperature += hc.currentTemperature;
        }

        Collider[] massColliders = Physics.OverlapSphere(transform.position, Mathf.Sqrt(totalMass) * 0.1f);
        float massNearCenter = 0;

        foreach (var col in massColliders)
        {
            if (col.TryGetComponent(out Flammable flammingMass) && flammingMass.isOnFire)
            {
                massNearCenter += flammingMass.heatContainer.mass;
            }
        }

        if (massNearCenter < totalMass * (4 / 5))
        {
            foreach (HeatContainer hc in contributers)
            {
                hc.heatBubble = null;
            }
            Debug.Log("I destroyed myself - not enough nearby");
            Destroy(gameObject);
            return;
        }

        centerOfMass = weightedSum / totalMass;
        averageTemperature = totalTemperature / contributers.Count;

        if (float.IsNaN(averageTemperature) || float.IsInfinity(averageTemperature))
        {
            Debug.LogError("Invalid averageTemperature");
            averageTemperature = 0f;
        }

        transform.position = centerOfMass;

        // Prune contributors that are too far
        float maxDistanceFactor = Mathf.Sqrt(totalMass) * 0.1f;
        float maxDistance = 0f;
        float minDistance = float.MaxValue;
        List<HeatContainer> toRemove = new List<HeatContainer>();

        foreach (HeatContainer hc in contributers)
        {
            if (hc == null) continue;
            float dist = Vector3.Distance(centerOfMass, hc.transform.position);
            minDistance = Mathf.Min(minDistance, dist);
            maxDistance = Mathf.Max(maxDistance, dist);
            if (dist > maxDistanceFactor)
                toRemove.Add(hc);
        }

        foreach (var hc in toRemove)
        {
            hc.heatBubble = null;
            contributers.Remove(hc);
            // resizing = false;
        }
        // contributers.RemoveAll(c => toRemove.Contains(c));

        if (contributers == null || contributers.Count == 0)
        {
            foreach (var hc in contributers)
            {
                hc.heatBubble = null;
            }
            Debug.Log("I destroyed myself - no one left2");
            Destroy(gameObject);
            return;
        }

        float massRadius = totalMass / (totalMass / Mathf.Sqrt(totalMass)) * 0.1f;

        if ((4 / 3) * Mathf.PI * Mathf.Pow(massRadius, 3) < bubbleStarter.volume)
        {
            radius = totalMass / (totalMass / Mathf.Sqrt(totalMass));
        }
        else
        {
            radius = massRadius;
        }

        if (bubble == null)
            bubble = GetComponent<SphereCollider>();

        bubble.radius = radius;
        HandleNoRigidbodies(radius);
        SyncVisualScale();

        // resizing = false;
        // if (resizeQueued)
        //     Resize();
    }


    void OnTriggerEnter(Collider other)
    {
        HeatContainer otherHeatContainer = other.GetComponent<HeatContainer>();
        if (otherHeatContainer != null)
        {
            if (otherHeatContainer.shouldApplyRadiativeHeating && otherHeatContainer.containerType != HeatContainer.ContainerType.Air)
            {
                // if (otherHeatContainer.heatBubble != null && otherHeatContainer.heatBubble.contributers.Count < 2)
                // {
                //     otherHeatContainer.heatBubble.RemoveContributer(otherHeatContainer);
                // }
                AddContributer(otherHeatContainer, true);
                AddTargetForHeating(otherHeatContainer);
            }
            else
            {
                AddTargetForHeating(otherHeatContainer);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        HeatContainer otherHeatContainer = other.GetComponent<HeatContainer>();
        if (otherHeatContainer != null)
        {
            if (otherHeatContainer.shouldApplyRadiativeHeating && contributers.Contains(otherHeatContainer))
            {
                otherHeatContainer.heatBubble = null;
                RemoveContributer(otherHeatContainer, true);
                // otherHeatContainer.createHeatBubble();
                RemoveTargetForHeating(otherHeatContainer);
            }
            else
            {
                RemoveTargetForHeating(otherHeatContainer);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        HeatContainer target = other.GetComponent<HeatContainer>();
        if (target == null || !targetsForHeating.Contains(target)) return;

        // Skip heating targets that are actively burning
        if (target.shouldApplyRadiativeHeating && target.currentTemperature >= target.heatMat.ignitionTemperature * target.heatMat.burnTemperatureMultiplier)
            return;

        // Physical constants
        const float stefanBoltzmannConstant = 5.67e-8f;
        const float timeStep = 1f / 60f; // Optional, can use Time.deltaTime directly

        // Distance attenuation
        float distance = Vector3.Distance(transform.position, target.transform.position) / radius;
        float distanceSquared = Mathf.Max(0.01f, distance * distance); // Prevent divide-by-zero

        // Radiation power emitted from the bubble and received by the target
        float radiativePower = stefanBoltzmannConstant * Mathf.Pow(averageTemperature + 273, 4) / distanceSquared;

        // Energy transferred over this frame
        float energy = radiativePower;

        // Apply to the target
        target.IncreaseHeat(this, energy);
    }

    void HandleNoRigidbodies(float radius)
    {
        bool needToResize = false;
        Collider[] colliders = Physics.OverlapSphere(transform.position, radius);
        foreach (var col in colliders)
        {
            if (col.TryGetComponent(out HeatContainer target) && !col.TryGetComponent(out Rigidbody targetRb))
            {
                if (target.shouldApplyRadiativeHeating)
                {
                    AddContributer(target, false);
                    needToResize = true;
                }
                else
                {
                    AddTargetForHeating(target);
                }
            }
        }
        // if (needToResize)
        // {
        //     Resize();
        // }
    }

}
