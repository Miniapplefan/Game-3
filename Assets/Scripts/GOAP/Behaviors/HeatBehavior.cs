using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

[RequireComponent(typeof(BodyState))]
public class HeatBehavior : MonoBehaviour
{
    private BodyState bodyState;
    private void Awake()
    {
        bodyState = GetComponentInChildren<BodyState>();
    }
}
