using System.Collections;
using System.Collections.Generic;
using CameraProjectionRenderingToolkit;
using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    BodyController bodyController;
    public GameObject heatGauge;
    Vector3 heatGaugeScaleCache;
    public TMP_Text auraIndicator;
    public TMP_Text tempIndicator;
    public TMP_Text tempExternalIndicator;
    public TMP_Text dollarsIndicator;
    public TMP_Text healthIndicator;
    public TMP_Text overheatIndicator;
    public RectTransform auraGripGauge;
    public SpriteRenderer bulletTimePulse1Sprite;
    public SpriteRenderer bulletTimePulse2Sprite;

    [Header("Aura Grip Threshold Ticks")]
    [SerializeField] private Transform auraGripThreshold1LeftTick;
    [SerializeField] private Transform auraGripThreshold1RightTick;
    [SerializeField] private Transform auraGripThreshold2LeftTick;
    [SerializeField] private Transform auraGripThreshold2RightTick;
    [SerializeField] private float auraGripThresholdTickDepthOffset = -0.001f;

    [Header("Bullet Time CPRT Feedback")]
    [SerializeField] private bool enableBulletTimeCprtFeedback = true;
    [SerializeField] private CPRT cprtTarget;
    [SerializeField, Range(0f, 1f)] private float bulletTimeCprtIntensity = 0.35f;
    [SerializeField, Min(0f)] private float cprtFeedbackEnterDuration = 0.08f;
    [SerializeField, Min(0f)] private float cprtFeedbackExitDuration = 0.2f;
    [SerializeField, Min(0f)] private float cprtRestoreLeadTime = 1f;

    [Header("Bullet Time Color Grading")]
    [SerializeField] private bool enableBulletTimeColorGrading = true;
    [SerializeField] private BulletTimeColorGradeEffect colorGradeTarget;
    [SerializeField] private Color bulletTimeColorTint = new Color(0.7215686f, 0.8509804f, 1f, 1f);
    [SerializeField, Range(0f, 2f)] private float bulletTimeColorSaturation = 0.55f;
    [SerializeField, Range(0f, 1f)] private float bulletTimeColorGradeIntensity = 0.55f;
    [SerializeField, Min(0f)] private float colorGradeEnterDuration = 0.2f;
    [SerializeField, Min(0f)] private float colorGradeExitDuration = 0.5f;
    [SerializeField, Min(0f)] private float colorGradeRestoreLeadTime = 1.4f;

    Color color;
    private const float PulseInvisibleAlpha = 0f;
    private const float PulseRechargingAlpha = 0.25f;
    private const float PulseReadyAlpha = 1f;
    private int observedBulletTimeTriggerVersion;
    private bool cprtFeedbackActive;
    private bool cprtFeedbackRestoring;
    private bool cprtTransitionActive;
    private float cprtBaselineIntensity;
    private float cprtTransitionStartIntensity;
    private float cprtTransitionTargetIntensity;
    private float cprtTransitionElapsed;
    private float cprtTransitionDuration;
    private int observedColorGradeTriggerVersion;
    private bool colorGradeFeedbackActive;
    private bool colorGradeFeedbackRestoring;
    private bool colorGradeTransitionActive;
    private Color colorGradeBaselineTint;
    private float colorGradeBaselineSaturation;
    private float colorGradeBaselineIntensity;
    private bool colorGradeBaselineEnabled;
    private Color colorGradeTransitionStartTint;
    private Color colorGradeTransitionTargetTint;
    private float colorGradeTransitionStartSaturation;
    private float colorGradeTransitionTargetSaturation;
    private float colorGradeTransitionStartIntensity;
    private float colorGradeTransitionTargetIntensity;
    private float colorGradeTransitionElapsed;
    private float colorGradeTransitionDuration;

    // Start is called before the first frame update
    void Start()
    {
        bodyController = GetComponent<BodyController>();
        heatGaugeScaleCache = heatGauge.transform.localScale;
        // healthIndicator.text = bodyController.head.health.ToString();
        // bodyController.heatContainer.OnOverheated += enableOverheatText;
        // bodyController.cooling.RaiseCooledDownFromOverheat += disableOverheatText;
        // disableOverheatText();
    }

    // Update is called once per frame
    void Update()
    {
        UpdateBulletTimeCprtFeedback();
        UpdateBulletTimeColorGrading();
    }

    void OnDisable()
    {
        RestoreColorGradeFeedbackIfNeeded(colorGradeTarget);
    }

    void FixedUpdate()
    {
        // displayHeatGauge();
        // displayDollarsGauage();
        // displayHealthGauge();
        // displayTempGauge();
        // displayTempExternalGauge();

        if (!bodyController.isAI)
        {
            displayAuraGauge();
            displayAuraGripGauge();
            displayAuraGripThresholdTicks();
            displayBulletTimePulseSprites();
        }
    }

    void displayAuraGauge()
    {
        auraIndicator.text = bodyController.auraManager.AuraFloat.ToString("0.0");
    }

    void displayAuraGripGauge()
    {
        if (auraGripGauge == null || bodyController.auraManager == null)
        {
            return;
        }

        Vector3 scale = auraGripGauge.localScale;
        scale.x = bodyController.auraManager.AuraGripNormalized;
        auraGripGauge.localScale = scale;
    }

    void displayAuraGripThresholdTicks()
    {
        if (auraGripGauge == null || auraGripGauge.parent == null || bodyController.auraManager == null)
        {
            return;
        }

        Rect gaugeRect = auraGripGauge.rect;
        Vector3 fullScale = auraGripGauge.localScale;
        fullScale.x = 1f;

        Vector3 centerInGaugeSpace = new Vector3(gaugeRect.center.x, gaugeRect.center.y, 0f);
        Vector3 rightEdgeInGaugeSpace = centerInGaugeSpace + Vector3.right * gaugeRect.width * 0.5f;
        Vector3 leftEdgeInGaugeSpace = centerInGaugeSpace - Vector3.right * gaugeRect.width * 0.5f;
        Vector3 depthOffsetInGaugeSpace = Vector3.forward * auraGripThresholdTickDepthOffset;

        Vector3 centerWorld = GaugePointToWorld(centerInGaugeSpace + depthOffsetInGaugeSpace, fullScale);
        Vector3 leftEdgeWorld = GaugePointToWorld(leftEdgeInGaugeSpace + depthOffsetInGaugeSpace, fullScale);
        Vector3 rightEdgeWorld = GaugePointToWorld(rightEdgeInGaugeSpace + depthOffsetInGaugeSpace, fullScale);

        SetMirroredThresholdTickPositions(
            auraGripThreshold1LeftTick,
            auraGripThreshold1RightTick,
            centerWorld,
            leftEdgeWorld,
            rightEdgeWorld,
            bodyController.auraManager.Threshold1PulseThreshold);
        SetMirroredThresholdTickPositions(
            auraGripThreshold2LeftTick,
            auraGripThreshold2RightTick,
            centerWorld,
            leftEdgeWorld,
            rightEdgeWorld,
            bodyController.auraManager.Threshold2PulseThreshold);
    }

    Vector3 GaugePointToWorld(Vector3 pointInGaugeSpace, Vector3 fullScale)
    {
        Vector3 pointInParentSpace = auraGripGauge.localPosition
            + auraGripGauge.localRotation * Vector3.Scale(pointInGaugeSpace, fullScale);
        return auraGripGauge.parent.TransformPoint(pointInParentSpace);
    }

    void SetMirroredThresholdTickPositions(
        Transform leftTick,
        Transform rightTick,
        Vector3 centerWorld,
        Vector3 leftEdgeWorld,
        Vector3 rightEdgeWorld,
        float normalizedThreshold)
    {
        float threshold = Mathf.Clamp01(normalizedThreshold);
        if (leftTick != null)
        {
            leftTick.position = Vector3.Lerp(centerWorld, leftEdgeWorld, threshold);
        }

        if (rightTick != null)
        {
            rightTick.position = Vector3.Lerp(centerWorld, rightEdgeWorld, threshold);
        }
    }

    void displayBulletTimePulseSprites()
    {
        if (bodyController.auraManager == null)
        {
            SetPulseSpriteAlpha(bulletTimePulse1Sprite, PulseInvisibleAlpha);
            SetPulseSpriteAlpha(bulletTimePulse2Sprite, PulseInvisibleAlpha);
            return;
        }

        AuraManager auraManager = bodyController.auraManager;
        displayBulletTimePulseSprite(
            bulletTimePulse1Sprite,
            auraManager.HasThreshold1Pulse,
            auraManager.AuraGripNormalized >= auraManager.Threshold1PulseThreshold);
        displayBulletTimePulseSprite(
            bulletTimePulse2Sprite,
            auraManager.HasThreshold2Pulse,
            auraManager.AuraGripNormalized >= auraManager.Threshold2PulseThreshold);
    }

    void displayBulletTimePulseSprite(SpriteRenderer pulseSprite, bool hasPulse, bool isAboveThreshold)
    {
        if (hasPulse)
        {
            SetPulseSpriteAlpha(pulseSprite, PulseReadyAlpha);
            return;
        }

        SetPulseSpriteAlpha(pulseSprite, isAboveThreshold ? PulseRechargingAlpha : PulseInvisibleAlpha);
    }

    void SetPulseSpriteAlpha(SpriteRenderer pulseSprite, float alpha)
    {
        if (pulseSprite == null)
        {
            return;
        }

        Color spriteColor = pulseSprite.color;
        spriteColor.a = alpha;
        pulseSprite.color = spriteColor;
    }

    void UpdateBulletTimeCprtFeedback()
    {
        if (bodyController == null || bodyController.isAI)
        {
            return;
        }

        CPRT cprt = ResolveCprtTarget();
        if (!enableBulletTimeCprtFeedback)
        {
            RestoreCprtFeedbackIfNeeded(cprt);
            return;
        }

        if (cprt == null)
        {
            return;
        }

        bool bulletTimeActive = BulletTimeManager.IsActive;
        int triggerVersion = BulletTimeManager.TriggerVersion;
        if (bulletTimeActive && triggerVersion != observedBulletTimeTriggerVersion)
        {
            observedBulletTimeTriggerVersion = triggerVersion;
            BeginCprtBulletTimeFeedback(cprt);
        }

        if (!cprtFeedbackActive)
        {
            return;
        }

        if (!bulletTimeActive)
        {
            FinishCprtBulletTimeFeedback(cprt);
            return;
        }

        float effectiveRestoreLead = Mathf.Min(
            Mathf.Max(0f, cprtRestoreLeadTime),
            BulletTimeManager.Duration * 0.5f);
        if (!cprtFeedbackRestoring && BulletTimeManager.RemainingTime <= effectiveRestoreLead)
        {
            cprtFeedbackRestoring = true;
            StartCprtTransition(cprt, cprtBaselineIntensity, cprtFeedbackExitDuration);
        }

        UpdateCprtTransition(cprt);
    }

    CPRT ResolveCprtTarget()
    {
        if (cprtTarget == null)
        {
            cprtTarget = FindObjectOfType<CPRT>();
        }

        return cprtTarget;
    }

    void BeginCprtBulletTimeFeedback(CPRT cprt)
    {
        if (cprt == null)
        {
            return;
        }

        if (!cprtFeedbackActive)
        {
            cprtBaselineIntensity = cprt.intensity;
        }

        cprtFeedbackActive = true;
        cprtFeedbackRestoring = false;
        StartCprtTransition(cprt, Mathf.Clamp01(bulletTimeCprtIntensity), cprtFeedbackEnterDuration);
    }

    void RestoreCprtFeedbackIfNeeded(CPRT cprt)
    {
        if (!cprtFeedbackActive || cprt == null)
        {
            cprtFeedbackActive = false;
            cprtTransitionActive = false;
            cprtFeedbackRestoring = false;
            return;
        }

        FinishCprtBulletTimeFeedback(cprt);
    }

    void FinishCprtBulletTimeFeedback(CPRT cprt)
    {
        if (cprt != null)
        {
            cprt.intensity = cprtBaselineIntensity;
        }

        cprtFeedbackActive = false;
        cprtFeedbackRestoring = false;
        cprtTransitionActive = false;
    }

    void StartCprtTransition(CPRT cprt, float targetIntensity, float duration)
    {
        cprtTransitionStartIntensity = cprt != null ? cprt.intensity : targetIntensity;
        cprtTransitionTargetIntensity = Mathf.Clamp01(targetIntensity);
        cprtTransitionElapsed = 0f;
        cprtTransitionDuration = Mathf.Max(0f, duration);
        cprtTransitionActive = true;
        if (cprtTransitionDuration <= 0f && cprt != null)
        {
            cprt.intensity = cprtTransitionTargetIntensity;
            cprtTransitionActive = false;
        }
    }

    void UpdateCprtTransition(CPRT cprt)
    {
        if (!cprtTransitionActive || cprt == null)
        {
            return;
        }

        cprtTransitionElapsed += Time.unscaledDeltaTime;
        float t = cprtTransitionDuration <= 0f
            ? 1f
            : Mathf.Clamp01(cprtTransitionElapsed / cprtTransitionDuration);
        cprt.intensity = Mathf.Lerp(cprtTransitionStartIntensity, cprtTransitionTargetIntensity, t);
        if (t >= 1f)
        {
            cprtTransitionActive = false;
        }
    }

    void UpdateBulletTimeColorGrading()
    {
        if (bodyController == null || bodyController.isAI)
        {
            return;
        }

        if (!enableBulletTimeColorGrading)
        {
            RestoreColorGradeFeedbackIfNeeded(colorGradeTarget);
            return;
        }

        BulletTimeColorGradeEffect colorGrade = ResolveColorGradeTarget();
        if (colorGrade == null)
        {
            return;
        }

        bool bulletTimeActive = BulletTimeManager.IsActive;
        int triggerVersion = BulletTimeManager.TriggerVersion;
        if (bulletTimeActive && triggerVersion != observedColorGradeTriggerVersion)
        {
            observedColorGradeTriggerVersion = triggerVersion;
            BeginColorGradeFeedback(colorGrade);
        }

        if (!colorGradeFeedbackActive)
        {
            return;
        }

        if (!bulletTimeActive)
        {
            FinishColorGradeFeedback(colorGrade);
            return;
        }

        float effectiveRestoreLead = Mathf.Min(
            Mathf.Max(0f, colorGradeRestoreLeadTime),
            BulletTimeManager.Duration * 0.5f);
        if (!colorGradeFeedbackRestoring && BulletTimeManager.RemainingTime <= effectiveRestoreLead)
        {
            colorGradeFeedbackRestoring = true;
            StartColorGradeTransition(
                colorGrade,
                colorGradeBaselineTint,
                colorGradeBaselineSaturation,
                colorGradeBaselineIntensity,
                colorGradeExitDuration);
        }

        UpdateColorGradeTransition(colorGrade);
    }

    BulletTimeColorGradeEffect ResolveColorGradeTarget()
    {
        if (colorGradeTarget != null)
        {
            return colorGradeTarget;
        }

        Camera targetCamera = null;
        CPRT cprt = ResolveCprtTarget();
        if (cprt != null)
        {
            targetCamera = cprt.GetComponent<Camera>();
        }

        if (targetCamera == null && bodyController != null)
        {
            Camera fallbackCamera = null;
            Camera[] childCameras = bodyController.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < childCameras.Length; i++)
            {
                Camera childCamera = childCameras[i];
                if (fallbackCamera == null)
                {
                    fallbackCamera = childCamera;
                }

                if (childCamera.enabled && childCamera.gameObject.activeInHierarchy)
                {
                    targetCamera = childCamera;
                    break;
                }
            }

            if (targetCamera == null)
            {
                targetCamera = fallbackCamera;
            }
        }

        if (targetCamera == null)
        {
            return null;
        }

        colorGradeTarget = targetCamera.GetComponent<BulletTimeColorGradeEffect>();
        if (colorGradeTarget == null)
        {
            colorGradeTarget = targetCamera.gameObject.AddComponent<BulletTimeColorGradeEffect>();
            colorGradeTarget.enabled = false;
        }

        return colorGradeTarget;
    }

    void BeginColorGradeFeedback(BulletTimeColorGradeEffect colorGrade)
    {
        if (colorGrade == null)
        {
            return;
        }

        if (!colorGradeFeedbackActive)
        {
            colorGradeBaselineTint = colorGrade.Tint;
            colorGradeBaselineSaturation = colorGrade.Saturation;
            colorGradeBaselineIntensity = colorGrade.Intensity;
            colorGradeBaselineEnabled = colorGrade.enabled;
        }

        colorGrade.enabled = true;
        colorGradeFeedbackActive = true;
        colorGradeFeedbackRestoring = false;
        StartColorGradeTransition(
            colorGrade,
            bulletTimeColorTint,
            Mathf.Clamp(bulletTimeColorSaturation, 0f, 2f),
            Mathf.Clamp01(bulletTimeColorGradeIntensity),
            colorGradeEnterDuration);
    }

    void RestoreColorGradeFeedbackIfNeeded(BulletTimeColorGradeEffect colorGrade)
    {
        if (!colorGradeFeedbackActive || colorGrade == null)
        {
            colorGradeFeedbackActive = false;
            colorGradeFeedbackRestoring = false;
            colorGradeTransitionActive = false;
            return;
        }

        FinishColorGradeFeedback(colorGrade);
    }

    void FinishColorGradeFeedback(BulletTimeColorGradeEffect colorGrade)
    {
        if (colorGrade != null)
        {
            colorGrade.Tint = colorGradeBaselineTint;
            colorGrade.Saturation = colorGradeBaselineSaturation;
            colorGrade.Intensity = colorGradeBaselineIntensity;
            colorGrade.enabled = colorGradeBaselineEnabled;
        }

        colorGradeFeedbackActive = false;
        colorGradeFeedbackRestoring = false;
        colorGradeTransitionActive = false;
    }

    void StartColorGradeTransition(
        BulletTimeColorGradeEffect colorGrade,
        Color targetTint,
        float targetSaturation,
        float targetIntensity,
        float duration)
    {
        colorGradeTransitionStartTint = colorGrade != null ? colorGrade.Tint : targetTint;
        colorGradeTransitionTargetTint = targetTint;
        colorGradeTransitionStartSaturation = colorGrade != null ? colorGrade.Saturation : targetSaturation;
        colorGradeTransitionTargetSaturation = Mathf.Clamp(targetSaturation, 0f, 2f);
        colorGradeTransitionStartIntensity = colorGrade != null ? colorGrade.Intensity : targetIntensity;
        colorGradeTransitionTargetIntensity = Mathf.Clamp01(targetIntensity);
        colorGradeTransitionElapsed = 0f;
        colorGradeTransitionDuration = Mathf.Max(0f, duration);
        colorGradeTransitionActive = true;

        if (colorGradeTransitionDuration <= 0f && colorGrade != null)
        {
            ApplyColorGradeTransition(colorGrade, 1f);
            colorGradeTransitionActive = false;
        }
    }

    void UpdateColorGradeTransition(BulletTimeColorGradeEffect colorGrade)
    {
        if (!colorGradeTransitionActive || colorGrade == null)
        {
            return;
        }

        colorGradeTransitionElapsed += Time.unscaledDeltaTime;
        float t = colorGradeTransitionDuration <= 0f
            ? 1f
            : Mathf.Clamp01(colorGradeTransitionElapsed / colorGradeTransitionDuration);
        ApplyColorGradeTransition(colorGrade, t);
        if (t >= 1f)
        {
            colorGradeTransitionActive = false;
        }
    }

    void ApplyColorGradeTransition(BulletTimeColorGradeEffect colorGrade, float t)
    {
        colorGrade.Tint = Color.Lerp(colorGradeTransitionStartTint, colorGradeTransitionTargetTint, t);
        colorGrade.Saturation = Mathf.Lerp(
            colorGradeTransitionStartSaturation,
            colorGradeTransitionTargetSaturation,
            t);
        colorGrade.Intensity = Mathf.Lerp(
            colorGradeTransitionStartIntensity,
            colorGradeTransitionTargetIntensity,
            t);
    }

    // void displayHeatGauge()
    // {
    //     heatGauge.transform.localScale = heatGaugeScaleCache * Mathf.Clamp((bodyController.heatContainer.currentTemperature + 0.01f) / bodyController.cooling.GetMaxHeat(), 0, 1f);
    // }

    void displayDollarsGauage()
    {
        dollarsIndicator.text = (Mathf.Round(bodyController.siphon.dollars * 100f) / 100f).ToString();
    }

    void displayHealthGauge()
    {
        healthIndicator.text = bodyController.head.health.ToString();
    }

    // void displayTempGauge()
    // {
    //     var temp = bodyController.heatContainer.currentTemperature;
    //     float normalizedTemp = Mathf.Clamp01((temp - 21) / 100f); // Normalize to a 0-1 range (adjust 100f as needed)
    //     tempIndicator.text = "T_in " + (int)temp + "°C";

    //     color = Color.Lerp(Color.blue, Color.red, normalizedTemp);
    //     tempIndicator.color = color;
    // }
    // void displayTempExternalGauge()
    // {
    //     var temp = bodyController.heatContainer.ambientTemperature;
    //     float normalizedTemp = Mathf.Clamp01((temp - 21) / 100f); // 
    //     tempExternalIndicator.text = "T_ex " + (int)temp + "°C";

    //     color = Color.Lerp(Color.blue, Color.red, normalizedTemp);
    //     tempExternalIndicator.color = color;
    // }

    void enableOverheatText()
    {
        overheatIndicator.gameObject.SetActive(true);
    }

    void disableOverheatText()
    {
        overheatIndicator.gameObject.SetActive(false);
    }
}
