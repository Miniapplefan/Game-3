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

    [System.Serializable]
    private class AuraGripPulseSlot
    {
        [Range(0f, 1f)] public float normalizedThreshold;
        public bool hasPulse;
        [Range(0f, 1f)] public float rechargeProgressNormalized;
        [System.NonSerialized] public bool wasAtOrAboveThreshold;

        public AuraGripPulseSlot()
        {
        }

        public AuraGripPulseSlot(float normalizedThreshold)
        {
            this.normalizedThreshold = normalizedThreshold;
        }
    }

    [SerializeField] private float baseAura = 1f;
    [SerializeField] private float currentAura = 1f;
    [SerializeField] private AnimationCurve auraDecayRateCurve = new AnimationCurve(
        new Keyframe(0f, 0.05f),
        new Keyframe(1f, 0.05f)
    );

    [Header("Extreme Aura Pressure")]
    [SerializeField] private bool enableAuraPressureDecay = true;
    [SerializeField] private float auraPressureThreshold = 4f;
    [SerializeField] private float auraPressureHalfLife = 4f;

    [Header("Graze")]
    [SerializeField] private int grazeAuraRewardTenths = 1;

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

    [Header("Bullet Time Pulses")]
    [SerializeField] private bool enableBulletTimePulses = true;
    [SerializeField] private float pulseRechargeDuration = 1f;
    [SerializeField] private AuraGripPulseSlot threshold1Pulse = new AuraGripPulseSlot(0.33f);
    [SerializeField] private AuraGripPulseSlot threshold2Pulse = new AuraGripPulseSlot(0.66f);

    public float AuraFloat => currentAura; // only for UI/inspection
    public float AuraGripNormalized => maxAuraGrip > 0f ? Mathf.Clamp01(currentAuraGrip / maxAuraGrip) : 0f;
    public bool HasThreshold1Pulse => threshold1Pulse != null && threshold1Pulse.hasPulse;
    public bool HasThreshold2Pulse => threshold2Pulse != null && threshold2Pulse.hasPulse;
    public float Threshold1PulseRechargeProgress => threshold1Pulse != null ? threshold1Pulse.rechargeProgressNormalized : 0f;
    public float Threshold2PulseRechargeProgress => threshold2Pulse != null ? threshold2Pulse.rechargeProgressNormalized : 0f;
    public float Threshold1PulseThreshold => threshold1Pulse != null ? threshold1Pulse.normalizedThreshold : 0f;
    public float Threshold2PulseThreshold => threshold2Pulse != null ? threshold2Pulse.normalizedThreshold : 0f;
    public int AvailableBulletTimePulseCount => (HasThreshold1Pulse ? 1 : 0) + (HasThreshold2Pulse ? 1 : 0);
    public bool HasAvailableBulletTimePulse => AvailableBulletTimePulseCount > 0;
    public event System.Action GrazeAwarded;

    private float timeSinceLastAuraGain;
    private float gripDecayAuraSnapshot;

    void Awake()
    {
        EnsurePulseSlots();
        InitializePulseThresholdStates();

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
        auraPressureThreshold = Mathf.Max(baseAura, auraPressureThreshold);
        auraPressureHalfLife = Mathf.Max(0.0001f, auraPressureHalfLife);
        grazeAuraRewardTenths = Mathf.Max(0, grazeAuraRewardTenths);
        currentAuraGrip = Mathf.Clamp(currentAuraGrip, 0f, Mathf.Max(0f, maxAuraGrip));
        maxAuraGrip = Mathf.Max(0f, maxAuraGrip);
        auraGripGainMultiplier = Mathf.Max(0f, auraGripGainMultiplier);
        gripDepletedThreshold = Mathf.Max(0f, gripDepletedThreshold);
        minGripHalfLife = Mathf.Max(0.0001f, minGripHalfLife);
        pulseRechargeDuration = Mathf.Max(0f, pulseRechargeDuration);
        ValidatePulseSlots();
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
        GrantPulsesForNewThresholdCrossings();
        gripDecayAuraSnapshot = currentAura;
        timeSinceLastAuraGain = 0f;
    }

    public bool TryRegisterGraze()
    {
        int rewardTenths = Mathf.Max(0, grazeAuraRewardTenths);
        if (rewardTenths <= 0)
        {
            return false;
        }

        AddAuraTenths(rewardTenths);
        GrazeAwarded?.Invoke();
        return true;
    }

    void Update()
    {
        if (currentAuraGrip > 0f)
        {
            DecayAuraGrip();
            UpdatePulseRecharge();
            return;
        }

        UpdatePulseRecharge();

        float auraDeltaTime = BulletTimeManager.GetDeltaTime(BulletTimeChannel.PlayerAura);
        timeSinceLastAuraGain += auraDeltaTime;

        if (currentAura <= baseAura)
        {
            currentAura = baseAura;
            return;
        }

        ApplyAuraPressureDecay(auraDeltaTime);

        float decayRate = Mathf.Max(0f, EvaluateDecayRate());
        currentAura = Mathf.Max(baseAura, currentAura - decayRate * auraDeltaTime);
    }

    public bool TryConsumeBulletTimePulse()
    {
        if (!enableBulletTimePulses)
        {
            return true;
        }

        EnsurePulseSlots();

        bool consumeThreshold2First = threshold2Pulse.normalizedThreshold >= threshold1Pulse.normalizedThreshold;
        if (consumeThreshold2First)
        {
            return TryConsumePulseSlot(threshold2Pulse) || TryConsumePulseSlot(threshold1Pulse);
        }

        return TryConsumePulseSlot(threshold1Pulse) || TryConsumePulseSlot(threshold2Pulse);
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

    private void UpdatePulseRecharge()
    {
        EnsurePulseSlots();

        float gripNormalized = AuraGripNormalized;
        float pulseDeltaTime = BulletTimeManager.GetDeltaTime(BulletTimeChannel.PlayerPulseRecharge);
        UpdatePulseSlotRecharge(threshold1Pulse, gripNormalized, pulseDeltaTime);
        UpdatePulseSlotRecharge(threshold2Pulse, gripNormalized, pulseDeltaTime);
    }

    private void UpdatePulseSlotRecharge(AuraGripPulseSlot slot, float gripNormalized, float pulseDeltaTime)
    {
        bool isAtOrAboveThreshold = UpdatePulseSlotThresholdState(slot, gripNormalized);
        if (slot.hasPulse)
        {
            slot.rechargeProgressNormalized = 1f;
            return;
        }

        slot.rechargeProgressNormalized = Mathf.Clamp01(slot.rechargeProgressNormalized);
        if (!isAtOrAboveThreshold)
        {
            return;
        }

        if (pulseRechargeDuration <= 0f)
        {
            FillPulseSlot(slot);
            return;
        }

        slot.rechargeProgressNormalized = Mathf.Clamp01(slot.rechargeProgressNormalized + pulseDeltaTime / pulseRechargeDuration);
        if (slot.rechargeProgressNormalized >= 1f)
        {
            FillPulseSlot(slot);
        }
    }

    private void InitializePulseThresholdStates()
    {
        float gripNormalized = AuraGripNormalized;
        threshold1Pulse.wasAtOrAboveThreshold = gripNormalized >= threshold1Pulse.normalizedThreshold;
        threshold2Pulse.wasAtOrAboveThreshold = gripNormalized >= threshold2Pulse.normalizedThreshold;
    }

    private void GrantPulsesForNewThresholdCrossings()
    {
        EnsurePulseSlots();

        float gripNormalized = AuraGripNormalized;
        UpdatePulseSlotThresholdState(threshold1Pulse, gripNormalized);
        UpdatePulseSlotThresholdState(threshold2Pulse, gripNormalized);
    }

    private bool UpdatePulseSlotThresholdState(AuraGripPulseSlot slot, float gripNormalized)
    {
        bool isAtOrAboveThreshold = gripNormalized >= slot.normalizedThreshold;
        bool crossedThreshold = isAtOrAboveThreshold && !slot.wasAtOrAboveThreshold;
        slot.wasAtOrAboveThreshold = isAtOrAboveThreshold;

        if (crossedThreshold)
        {
            FillPulseSlot(slot);
        }

        return isAtOrAboveThreshold;
    }

    private bool TryConsumePulseSlot(AuraGripPulseSlot slot)
    {
        if (slot == null || !slot.hasPulse)
        {
            return false;
        }

        slot.hasPulse = false;
        slot.rechargeProgressNormalized = 0f;
        return true;
    }

    private void FillPulseSlot(AuraGripPulseSlot slot)
    {
        slot.hasPulse = true;
        slot.rechargeProgressNormalized = 1f;
    }

    private float EvaluateDecayRate()
    {
        if (auraDecayRateCurve == null || auraDecayRateCurve.length == 0)
        {
            return 0f;
        }

        return auraDecayRateCurve.Evaluate(timeSinceLastAuraGain);
    }

    private void ApplyAuraPressureDecay(float auraDeltaTime)
    {
        if (!enableAuraPressureDecay || currentAura <= auraPressureThreshold)
        {
            return;
        }

        float halfLife = Mathf.Max(0.0001f, auraPressureHalfLife);
        float excessAura = currentAura - auraPressureThreshold;
        float decayMultiplier = Mathf.Pow(0.5f, auraDeltaTime / halfLife);
        currentAura = auraPressureThreshold + excessAura * decayMultiplier;
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

    private void EnsurePulseSlots()
    {
        if (threshold1Pulse == null)
        {
            threshold1Pulse = new AuraGripPulseSlot(0.33f);
        }

        if (threshold2Pulse == null)
        {
            threshold2Pulse = new AuraGripPulseSlot(0.66f);
        }
    }

    private void ValidatePulseSlots()
    {
        EnsurePulseSlots();
        ValidatePulseSlot(threshold1Pulse);
        ValidatePulseSlot(threshold2Pulse);

        if (threshold2Pulse.normalizedThreshold < threshold1Pulse.normalizedThreshold)
        {
            threshold2Pulse.normalizedThreshold = threshold1Pulse.normalizedThreshold;
        }
    }

    private void ValidatePulseSlot(AuraGripPulseSlot slot)
    {
        slot.normalizedThreshold = Mathf.Clamp01(slot.normalizedThreshold);
        slot.rechargeProgressNormalized = Mathf.Clamp01(slot.rechargeProgressNormalized);

        if (slot.hasPulse || slot.rechargeProgressNormalized >= 1f)
        {
            FillPulseSlot(slot);
        }
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
