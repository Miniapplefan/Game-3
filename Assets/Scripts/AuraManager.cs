using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AuraManager : MonoBehaviour
{
    // Aura is stored in tenths. 12 == 1.2
    [SerializeField] private int baseAuraTenth = 10;        // 1.0
    [SerializeField] private int auraTenth = 10;            // current aura
    [SerializeField] private int auraDecayPerTickTenth = 1; // 0.1 per tick
    [SerializeField] private float auraDecayTick = 2f;   // seconds
    public float AuraFloat => auraTenth / 10f; // only for UI/inspection

    private float decayTimer;

    void Update()
    {
        decayTimer += Time.deltaTime;
        while (decayTimer >= auraDecayTick)
        {
            decayTimer -= auraDecayTick;

            auraTenth = Mathf.Max(baseAuraTenth, auraTenth - auraDecayPerTickTenth);
        }
    }
}
