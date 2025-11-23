using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SiphonTarget : MonoBehaviour
{
	public float dollarsPerSecond = 1;
	public float dollarAmount = 100;
	public float dollarsLeft = 1;
	public SiphonModel siphoner;
	public GameObject siphonerGameObject;
	public GameObject dollarsLeftIndicator;
	public LineRenderer siphonerLine;

	public Material targeted;
	public Material idle;
	Vector3 scaleCache;

	// Start is called before the first frame update
	void Start()
	{
		dollarsLeft = dollarAmount;
		scaleCache = dollarsLeftIndicator.transform.localScale;
		siphonerLine.positionCount = 2; // A line has two points
		siphonerLine.startWidth = 0.2f; // Adjust the line width as needed
		siphonerLine.endWidth = 0.1f;
	}

	// Update is called once per frame
	void Update()
	{
		if (siphoner != null)
		{
			if (dollarsLeft > 0)
			{
				siphoner.addDollars(Time.deltaTime * dollarsPerSecond * siphoner.getSiphoningRate());
				dollarsLeft -= Time.deltaTime * dollarsPerSecond * siphoner.getSiphoningRate();
				dollarsLeftIndicator.transform.localScale = scaleCache * (dollarsLeft / dollarAmount);
				//Debug.Log(siphoner.dollars);
				DrawLine(dollarsLeftIndicator.transform.position, siphoner.head.gameObject.transform.position);
			}
			if (Vector3.Distance(transform.position, siphoner.head.transform.position) > siphoner.getMaxSiphonDistance() || dollarsLeft <= 0)
			{
				notBeingSiphoned(siphoner);
			}
		}
	}

	public void beingSiphoned(SiphonModel siphonModel)
	{
		//this.GetComponent<MeshRenderer>().material = targeted;
		dollarsLeftIndicator.GetComponent<MeshRenderer>().material = targeted;
		siphonModel.extended = true;
		siphonModel.siphonTarget = this;
		siphoner = siphonModel;

		siphonerGameObject = siphoner.arm.gameObject;
	}

	public void notBeingSiphoned(SiphonModel siphonModel)
	{
		this.GetComponent<MeshRenderer>().material = idle;
		dollarsLeftIndicator.GetComponent<MeshRenderer>().material = idle;
		DrawLine(transform.position, transform.position);
		if (siphoner != null)
		{
			siphoner.extended = false;
			siphoner.siphonTarget = null;
		}

		siphonModel.extended = false;
		siphonModel.siphonTarget = null;

		siphonerGameObject = null;

		siphoner = null;
	}

	public void DrawLine(Vector3 startPosition, Vector3 endPosition)
	{
		// Set the positions of the LineRenderer
		siphonerLine.SetPosition(0, startPosition);
		siphonerLine.SetPosition(1, endPosition);
	}
}
