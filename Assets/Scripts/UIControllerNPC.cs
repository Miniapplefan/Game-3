using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIControllerNPC : MonoBehaviour
{
    public GameObject leftAimIndicatorLine;
    public GameObject rightAimIndicatorLine;
    public GameObject topAimIndicatorLine;
    public GameObject bottomAimIndicatorLine;
    public BodyState bodyState;
    public AttackConfigSO AttackConfig;

    bool aimIndicatorsVisible = true;


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
}
