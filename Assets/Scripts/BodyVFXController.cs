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

    public void doBloodParticles(Vector3 hitLocation, Quaternion hitNormal)
    {
        var blood = Instantiate(BloodParticles, hitLocation, hitNormal).gameObject;
        blood.transform.SetParent(this.gameObject.transform);
        Destroy(blood, 2f);
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
