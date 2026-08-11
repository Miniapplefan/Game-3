using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIControllerNPC : MonoBehaviour
{
    public GameObject leftAimIndicatorLine;
    public GameObject rightAimIndicatorLine;
    public GameObject topAimIndicatorLine;
    public GameObject bottomAimIndicatorLine;
    public GameObject AimIndicator;
    public TMP_Text healthIndicator;
    public TMP_Text damageIndicator;
    public Image healthBar;
    public Image healthBarDelta;
    public BodyState bodyState;
    public AttackConfigSO AttackConfig;

    const float HealthBarDeltaDisplayDuration = 2f;
    const float DamageIndicatorDisplayDuration = 2f;

    bool aimIndicatorsVisible = true;
    bool aimAssistIndicatorVisible = false;
    bool healthIndicatorVisible = true;
    bool damageIndicatorVisible = false;
    bool healthBarVisible = false;
    bool healthBarDeltaVisible = false;
    bool hasTakenDamage = false;
    float damageIndicatorHideTimer = 0f;
    float healthBarDeltaHideTimer = 0f;
    float lastDisplayedHealth = float.NaN;
    float maxHealth = float.NaN;
    AimAssistTarget aimAssistTarget;
    BodyController npcBodyController;
    BodyController playerBodyController;


    void Awake()
    {
        aimAssistTarget = GetComponent<AimAssistTarget>();
        npcBodyController = GetComponent<BodyController>();
        ResolvePlayerBodyController();

        if (AimIndicator != null)
        {
            AimIndicator.SetActive(false);
        }

        if (healthBar != null)
        {
            healthBar.gameObject.SetActive(false);
        }

        if (healthBarDelta != null)
        {
            healthBarDelta.gameObject.SetActive(false);
        }

        if (damageIndicator != null)
        {
            damageIndicator.gameObject.SetActive(false);
        }
    }


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (bodyState != null && bodyState.isDead)
        {
            SetAimIndicatorsActive(false);
            SetHealthIndicatorActive(false);
            SetHealthBarActive(false);
            SetHealthBarDeltaActive(false);
            SetDamageIndicatorActive(false);
            return;
        }

        SetHealthIndicatorActive(true);
        SetHealthBarActive(hasTakenDamage);
        UpdateHealthBarDeltaTimer();
        UpdateDamageIndicatorTimer();
        UpdateHealthIndicator();

        if (bodyState == null || AttackConfig == null || Mathf.Approximately(AttackConfig.TimeToAim, 0f))
        {
            return;
        }

        float aimProgress01 = Mathf.Clamp01(1f - (bodyState.TimeToAim / AttackConfig.TimeToAim));
        float dist = Mathf.Lerp(3f, 0.8f, aimProgress01);

        // Preserve the other axes from each line's current LOCAL position
        var lp = leftAimIndicatorLine.transform.localPosition;
        leftAimIndicatorLine.transform.localPosition = new Vector3(-dist, lp.y, lp.z);

        lp = rightAimIndicatorLine.transform.localPosition;
        rightAimIndicatorLine.transform.localPosition = new Vector3(dist, lp.y, lp.z);

        lp = topAimIndicatorLine.transform.localPosition;
        topAimIndicatorLine.transform.localPosition = new Vector3(lp.x, dist, lp.z);

        lp = bottomAimIndicatorLine.transform.localPosition;
        bottomAimIndicatorLine.transform.localPosition = new Vector3(lp.x, -dist, lp.z);
    }

    void LateUpdate()
    {
        if (aimAssistTarget != null)
        {
            return;
        }

        UpdateAimAssistIndicator();
    }

    void UpdateAimAssistIndicator()
    {
        if (playerBodyController == null)
        {
            ResolvePlayerBodyController();
        }

        bool shouldShow = bodyState != null
            && !bodyState.isDead
            && npcBodyController != null
            && playerBodyController != null
            && playerBodyController.BreakoutAimAssistPreviewBodyTarget == npcBodyController;
        SetAimAssistIndicatorActive(shouldShow);
    }

    void ResolvePlayerBodyController()
    {
        PlayerController playerController = FindObjectOfType<PlayerController>();
        if (playerController != null)
        {
            playerBodyController = playerController.GetComponent<BodyController>();
        }
    }

    void SetAimAssistIndicatorActive(bool active)
    {
        if (aimAssistIndicatorVisible == active)
        {
            return;
        }

        aimAssistIndicatorVisible = active;
        if (AimIndicator != null)
        {
            AimIndicator.SetActive(active);
        }
    }

    void SetAimIndicatorsActive(bool active)
    {
        if (aimIndicatorsVisible == active)
        {
            return;
        }

        aimIndicatorsVisible = active;

        if (leftAimIndicatorLine != null) leftAimIndicatorLine.SetActive(active);
        if (rightAimIndicatorLine != null) rightAimIndicatorLine.SetActive(active);
        if (topAimIndicatorLine != null) topAimIndicatorLine.SetActive(active);
        if (bottomAimIndicatorLine != null) bottomAimIndicatorLine.SetActive(active);
    }

    void SetHealthIndicatorActive(bool active)
    {
        if (healthIndicatorVisible == active)
        {
            return;
        }

        healthIndicatorVisible = active;

        if (healthIndicator != null) healthIndicator.gameObject.SetActive(active);
    }

    void SetHealthBarActive(bool active)
    {
        if (healthBarVisible == active)
        {
            return;
        }

        healthBarVisible = active;

        if (healthBar != null) healthBar.gameObject.SetActive(active);
    }

    void SetHealthBarDeltaActive(bool active)
    {
        if (healthBarDeltaVisible == active)
        {
            return;
        }

        healthBarDeltaVisible = active;

        if (healthBarDelta != null) healthBarDelta.gameObject.SetActive(active);
    }

    void ShowHealthBarDelta(float healthBeforeDamage)
    {
        if (!healthBarDeltaVisible && healthBarDelta != null)
        {
            healthBarDelta.fillAmount = maxHealth > 0f
                ? Mathf.Clamp01(healthBeforeDamage / maxHealth)
                : 0f;
        }

        healthBarDeltaHideTimer = HealthBarDeltaDisplayDuration;
        SetHealthBarDeltaActive(true);
    }

    void UpdateHealthBarDeltaTimer()
    {
        if (!healthBarDeltaVisible)
        {
            return;
        }

        healthBarDeltaHideTimer -= Time.deltaTime;
        if (healthBarDeltaHideTimer <= 0f)
        {
            healthBarDeltaHideTimer = 0f;
            SetHealthBarDeltaActive(false);
        }
    }

    void SetDamageIndicatorActive(bool active)
    {
        if (damageIndicatorVisible == active)
        {
            return;
        }

        damageIndicatorVisible = active;

        if (damageIndicator != null) damageIndicator.gameObject.SetActive(active);
    }

    void ShowDamageIndicator(float damageAmount)
    {
        if (damageIndicator != null)
        {
            damageIndicator.text = $"-{damageAmount:0.#}";
        }

        damageIndicatorHideTimer = DamageIndicatorDisplayDuration;
        SetDamageIndicatorActive(true);
    }

    void UpdateDamageIndicatorTimer()
    {
        if (!damageIndicatorVisible)
        {
            return;
        }

        damageIndicatorHideTimer -= Time.deltaTime;
        if (damageIndicatorHideTimer <= 0f)
        {
            damageIndicatorHideTimer = 0f;
            SetDamageIndicatorActive(false);
        }
    }

    void UpdateHealthIndicator()
    {
        if (bodyState == null || bodyState.head == null)
        {
            return;
        }

        float currentHealth = bodyState.head.currentHealth;

        if (float.IsNaN(maxHealth))
        {
            maxHealth = Mathf.Max(0f, currentHealth);
        }

        if (!hasTakenDamage && currentHealth < maxHealth && !Mathf.Approximately(currentHealth, maxHealth))
        {
            hasTakenDamage = true;
            SetHealthBarActive(true);
        }

        bool healthDecreased = !float.IsNaN(lastDisplayedHealth)
            && currentHealth < lastDisplayedHealth
            && !Mathf.Approximately(currentHealth, lastDisplayedHealth);

        if (healthDecreased)
        {
            ShowHealthBarDelta(lastDisplayedHealth);
            // Temporarily disabled. Keep the damage indicator implementation available for later.
            // ShowDamageIndicator(lastDisplayedHealth - currentHealth);
        }

        if (Mathf.Approximately(currentHealth, lastDisplayedHealth))
        {
            return;
        }

        lastDisplayedHealth = currentHealth;

        if (healthIndicator != null)
        {
            healthIndicator.text = currentHealth.ToString("0.#");
        }

        if (healthBar != null)
        {
            healthBar.fillAmount = maxHealth > 0f
                ? Mathf.Clamp01(currentHealth / maxHealth)
                : 0f;
        }
    }
}
