using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class UIController : MonoBehaviour
{
    BodyController bodyController;
    public GameObject heatGauge;
    Vector3 heatGaugeScaleCache;
    public TMP_Text tempIndicator;
    public TMP_Text tempExternalIndicator;
    public TMP_Text dollarsIndicator;
    public TMP_Text healthIndicator;
    public TMP_Text overheatIndicator;

    Color color;

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

    }

    void FixedUpdate()
    {
        // displayHeatGauge();
        displayDollarsGauage();
        displayHealthGauge();
        // displayTempGauge();
        // displayTempExternalGauge();
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
