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

    float AimProgress01;


    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
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
}
