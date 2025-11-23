using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlameZone : MonoBehaviour
{
    private Flammable flammableParent;

    private void Awake()
    {
        // Look up the Flammable script in the parent
        flammableParent = GetComponentInParent<Flammable>();
    }

    private void OnTriggerStay(Collider other)
    {
        var hc = other.gameObject.GetComponent<HeatContainer>();
        if (flammableParent != null && hc != null)
        {
            flammableParent.HandleFlameZoneStay(hc);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var hc = other.gameObject.GetComponent<HeatContainer>();
        if (flammableParent != null && hc != null)
        {
            flammableParent.HandleFlameZoneEnter(hc);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var hc = other.gameObject.GetComponent<HeatContainer>();
        if (flammableParent != null && hc != null)
        {
            flammableParent.HandleFlameZoneExit(hc);
        }
    }
}
