using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaterPool : MonoBehaviour
{
	public HeatContainer container;
	[SerializeField] private Renderer meshRenderer;
	[SerializeField] private Color minHeatColor = Color.blue;
	[SerializeField] private Color maxHeatColor = Color.red;
	private Material material;


	void Start()
	{
		container = GetComponent<HeatContainer>();
		if (meshRenderer == null)
		{
			meshRenderer = GetComponent<Renderer>();
		}
		material = meshRenderer.material;

	}

	void Update()
	{
		UpdateColorBasedOnHeat();
	}

	private void UpdateColorBasedOnHeat()
	{
		if (container != null && material != null)
		{
			float heatPercentage = Mathf.Clamp01(container.currentTemperature / container.maxTemperature);
			Color currentColor = Color.Lerp(minHeatColor, maxHeatColor, heatPercentage);
			material.SetColor("_Color", currentColor);
		}
	}

}
