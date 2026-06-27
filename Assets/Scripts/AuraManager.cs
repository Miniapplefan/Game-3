using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuraManager : MonoBehaviour
{
    private enum AuraGripDrainMode
    {
        HalfLife,
        Linear
    }

    [SerializeField] private float baseAura = 1f;
    [SerializeField] private float currentAura = 1f;
    [SerializeField] private AnimationCurve auraDecayRateCurve = new AnimationCurve(
        new Keyframe(0f, 0.05f),
        new Keyframe(1f, 0.05f)
    );
    [SerializeField] private float currentAuraGrip;
    [SerializeField] private float maxAuraGrip = 3f;
    [SerializeField] private float auraGripGainMultiplier = 1f;
    [SerializeField] private AnimationCurve gripHalfLifeByAura = new AnimationCurve(
        new Keyframe(1f, 3f),
        new Keyframe(4f, 1f)
    );
    [SerializeField] private AuraGripDrainMode auraGripDrainMode = AuraGripDrainMode.HalfLife;
    [SerializeField] private AnimationCurve linearGripDrainRateByAura = new AnimationCurve(
        new Keyframe(1f, 0.7f),
        new Keyframe(4f, 2.1f)
    );
    [SerializeField] private float gripDepletedThreshold = 0.01f;
    [SerializeField] private float minGripHalfLife = 0.01f;

    public float AuraFloat => currentAura; // only for UI/inspection
    public float AuraGripNormalized => maxAuraGrip > 0f ? Mathf.Clamp01(currentAuraGrip / maxAuraGrip) : 0f;

    private float timeSinceLastAuraGain;
    private float gripDecayAuraSnapshot;

    void Awake()
    {
        if (currentAuraGrip > 0f)
        {
            gripDecayAuraSnapshot = currentAura;
        }

        ConfigureDecayCurveWrapMode();
    }

    void OnValidate()
    {
        baseAura = Mathf.Max(0f, baseAura);
        currentAura = Mathf.Max(baseAura, currentAura);
        currentAuraGrip = Mathf.Clamp(currentAuraGrip, 0f, Mathf.Max(0f, maxAuraGrip));
        maxAuraGrip = Mathf.Max(0f, maxAuraGrip);
        auraGripGainMultiplier = Mathf.Max(0f, auraGripGainMultiplier);
        gripDepletedThreshold = Mathf.Max(0f, gripDepletedThreshold);
        minGripHalfLife = Mathf.Max(0.0001f, minGripHalfLife);
        ConfigureDecayCurveWrapMode();
    }

    public void AddAuraTenths(int amountTenth)
    {
        if (amountTenth <= 0)
        {
            return;
        }

        float auraAmount = amountTenth / 10f;
        currentAura += auraAmount;
        float gripCap = Mathf.Max(0f, maxAuraGrip);
        currentAuraGrip = Mathf.Clamp(currentAuraGrip + auraAmount * Mathf.Max(0f, auraGripGainMultiplier), 0f, gripCap);
        gripDecayAuraSnapshot = currentAura;
        timeSinceLastAuraGain = 0f;
    }

    void Update()
    {
        if (currentAuraGrip > 0f)
        {
            DecayAuraGrip();
            return;
        }

        float auraDeltaTime = BulletTimeManager.GetDeltaTime(BulletTimeChannel.PlayerAura);
        timeSinceLastAuraGain += auraDeltaTime;

        if (currentAura <= baseAura)
        {
            currentAura = baseAura;
            return;
        }

        float decayRate = Mathf.Max(0f, EvaluateDecayRate());
        currentAura = Mathf.Max(baseAura, currentAura - decayRate * auraDeltaTime);
    }

    private void DecayAuraGrip()
    {
        float auraGripDeltaTime = BulletTimeManager.GetDeltaTime(BulletTimeChannel.PlayerAuraGrip);

        switch (auraGripDrainMode)
        {
            case AuraGripDrainMode.Linear:
                DecayAuraGripLinear(auraGripDeltaTime);
                break;
            case AuraGripDrainMode.HalfLife:
            default:
                DecayAuraGripHalfLife(auraGripDeltaTime);
                break;
        }

        ClearAuraGripIfDepleted();
    }

    private void DecayAuraGripHalfLife(float auraGripDeltaTime)
    {
        float halfLife = Mathf.Max(minGripHalfLife, EvaluateGripHalfLife());
        float decayMultiplier = Mathf.Pow(0.5f, auraGripDeltaTime / halfLife);
        currentAuraGrip *= decayMultiplier;
    }

    private void DecayAuraGripLinear(float auraGripDeltaTime)
    {
        float drainRate = Mathf.Max(0f, EvaluateLinearGripDrainRate());
        currentAuraGrip -= drainRate * auraGripDeltaTime;
    }

    private void ClearAuraGripIfDepleted()
    {
        if (currentAuraGrip <= gripDepletedThreshold)
        {
            currentAuraGrip = 0f;
            timeSinceLastAuraGain = 0f;
        }
    }

    private float EvaluateDecayRate()
    {
        if (auraDecayRateCurve == null || auraDecayRateCurve.length == 0)
        {
            return 0f;
        }

        return auraDecayRateCurve.Evaluate(timeSinceLastAuraGain);
    }

    private float EvaluateGripHalfLife()
    {
        if (gripHalfLifeByAura == null || gripHalfLifeByAura.length == 0)
        {
            return minGripHalfLife;
        }

        return gripHalfLifeByAura.Evaluate(gripDecayAuraSnapshot);
    }

    private float EvaluateLinearGripDrainRate()
    {
        if (linearGripDrainRateByAura == null || linearGripDrainRateByAura.length == 0)
        {
            return 0f;
        }

        return linearGripDrainRateByAura.Evaluate(gripDecayAuraSnapshot);
    }

    private void ConfigureDecayCurveWrapMode()
    {
        if (auraDecayRateCurve != null)
        {
            auraDecayRateCurve.postWrapMode = WrapMode.ClampForever;
        }

        if (gripHalfLifeByAura != null)
        {
            gripHalfLifeByAura.postWrapMode = WrapMode.ClampForever;
        }

        if (linearGripDrainRateByAura != null)
        {
            linearGripDrainRateByAura.postWrapMode = WrapMode.ClampForever;
        }
    }
}
