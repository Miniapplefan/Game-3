using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BodyVFXController : MonoBehaviour
{
    private BodyController bodyController;
    public Transform OverheatParticleTransform1;
    public Transform OverheatParticleTransform2;
    public Transform DissipateParticleTransform1;
    public Transform DissipateParticleTransform2;
    public ParticleSystem HeatDissipationParticles;
    public ParticleSystem BloodParticles;
    [Header("Blood Particle Randomization")]
    [SerializeField, Range(0f, 89f)] private float minimumBloodTilt = 10f;
    [SerializeField, Range(0f, 89f)] private float maximumBloodTilt = 30f;
    [SerializeField, Min(0f)] private float minimumBloodStartSpeed = 9f;
    [SerializeField, Min(0f)] private float maximumBloodStartSpeed = 11f;
    private ParticleSystem OverheatParticle1;
    private ParticleSystem OverheatParticle2;
    private ParticleSystem DissipateParticle1;
    private ParticleSystem DissipateParticle2;

    private float lastHeat = 0;
    private float currentHeat = 0;
    private float coolingRate = 0f;

    // Start is called before the first frame update
    void Start()
    {
        bodyController = GetComponentInParent<BodyController>();
        // SubscribeToEvents();
        // createOverheatParticles();
        // createDissipateParticles();
        // StartCoroutine(getCoolingRate());
    }

    // Update is called once per frame
    void Update()
    {

    }

    // IEnumerator getCoolingRate()
    // {
    //     currentHeat = bodyController.heatContainer.currentTemperature + 0.01f;
    //     coolingRate = lastHeat - currentHeat / Time.deltaTime;
    //     lastHeat = bodyController.heatContainer.currentTemperature + 0.01f;
    //     //Debug.Log(coolingRate);
    //     var emissionRate = (Mathf.Abs(coolingRate) / 25000) * 15;
    //     var speed = (Mathf.Abs(coolingRate) / 25000) * 3f;
    //     var emission = DissipateParticle1.emission;
    //     var main = DissipateParticle1.main;
    //     emission.rateOverTime = emissionRate;
    //     main.startSpeed = speed;

    //     emission = DissipateParticle2.emission;
    //     main = DissipateParticle2.main;
    //     emission.rateOverTime = emissionRate;
    //     main.startSpeed = speed;
    //     yield return new WaitForSeconds(0.1f);
    //     StartCoroutine(getCoolingRate());
    // }

    // void SubscribeToEvents()
    // {
    //     bodyController.heatContainer.OnOverheated += playOverheatParticles;
    //     bodyController.cooling.RaiseCooledDownFromOverheat += stopOverheatParticles;
    // }

    // void createOverheatParticles()
    // {
    //     OverheatParticle1 = Instantiate(HeatDissipationParticles, OverheatParticleTransform1);
    //     OverheatParticle2 = Instantiate(HeatDissipationParticles, OverheatParticleTransform2);
    //     OverheatParticle1.Stop();
    //     OverheatParticle2.Stop();
    // }

    // void createDissipateParticles()
    // {
    //     DissipateParticle1 = Instantiate(HeatDissipationParticles, DissipateParticleTransform1);
    //     DissipateParticle2 = Instantiate(HeatDissipationParticles, DissipateParticleTransform2);
    //     DissipateParticle1.Play();
    //     DissipateParticle2.Play();

    //     var emission = DissipateParticle1.emission;
    //     emission.rateOverTime = 0;

    //     emission = DissipateParticle2.emission;
    //     emission.rateOverTime = 0;
    // }

    public void doBloodParticles(Vector3 hitLocation, Vector3 hitNormal)
    {
        const float directionEpsilon = 0.000001f;

        Vector3 surfaceNormal = hitNormal.sqrMagnitude > directionEpsilon
            ? hitNormal.normalized
            : transform.forward;

        Vector3 surfaceTangent = Vector3.ProjectOnPlane(Random.onUnitSphere, surfaceNormal);
        if (surfaceTangent.sqrMagnitude <= directionEpsilon)
        {
            Vector3 fallbackAxis = Mathf.Abs(surfaceNormal.y) < 0.999f
                ? Vector3.up
                : Vector3.right;
            surfaceTangent = Vector3.Cross(surfaceNormal, fallbackAxis);
        }
        surfaceTangent.Normalize();

        float side = Random.value < 0.5f ? -1f : 1f;
        float minimumTilt = Mathf.Clamp(Mathf.Min(minimumBloodTilt, maximumBloodTilt), 0f, 89f);
        float maximumTilt = Mathf.Clamp(Mathf.Max(minimumBloodTilt, maximumBloodTilt), 0f, 89f);
        float tiltRadians = Random.Range(minimumTilt, maximumTilt) * Mathf.Deg2Rad;
        Vector3 burstDirection = (
            surfaceNormal * Mathf.Cos(tiltRadians)
            + surfaceTangent * side * Mathf.Sin(tiltRadians)
        ).normalized;
        Quaternion burstRotation = Quaternion.FromToRotation(Vector3.forward, burstDirection);

        ParticleSystem blood = Instantiate(BloodParticles, hitLocation, burstRotation);
        blood.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        blood.transform.SetParent(transform);

        float minimumStartSpeed = Mathf.Max(0f, Mathf.Min(minimumBloodStartSpeed, maximumBloodStartSpeed));
        float maximumStartSpeed = Mathf.Max(0f, Mathf.Max(minimumBloodStartSpeed, maximumBloodStartSpeed));
        var main = blood.main;
        main.startSpeed = new ParticleSystem.MinMaxCurve(minimumStartSpeed, maximumStartSpeed);

        blood.Play(true);
        StartCoroutine(UpdateParticleSimulationSpeed(blood));
        Destroy(blood.gameObject, 2f);
    }

    private IEnumerator UpdateParticleSimulationSpeed(ParticleSystem particleSystem)
    {
        while (particleSystem != null)
        {
            var main = particleSystem.main;
            main.simulationSpeed = BulletTimeManager.GetScale(BulletTimeChannel.VFX);
            yield return null;
        }
    }

    // public void playOverheatParticles()
    // {
    //     OverheatParticle1.Play();
    //     OverheatParticle2.Play();
    // }

    // public void stopOverheatParticles()
    // {
    //     OverheatParticle1.Stop();
    //     OverheatParticle2.Stop();
    // }
}
