using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SiphonModel : SystemModel
{
	public SiphonTarget siphonTarget;
	public bool extended = false;
	public Transform head;

	public Transform arm;

	float extendedTime = 1;
	float retractTime = 3;
	float currentTimer = 0;

	float maxExtendDistance = 2;
	float maxSiphonDistanceMultiplier = 5;


	public int siphonLayerMask = ~((1 << 6) | (1 << 7));

	public float dollars = 0;

	public SiphonModel(int currentLvl, Transform h, Transform a) : base(currentLvl)
	{
		head = h;
		arm = a;
	}

	public override void SetNameAndMaxLevel()
	{
		name = BodyInfo.systemID.Siphon;
		maxLevel = 6;
	}

	protected override void InitCommands()
	{
	}

	bool isLookingAtSiphonTarget()
	{
		RaycastHit hit;
		if (Physics.Raycast(head.position, head.forward, out hit, maxExtendDistance, siphonLayerMask))
		{
			// Debug.Log(hit.transform.gameObject.layer);
			if (hit.transform.gameObject.GetComponent<SiphonTarget>() != null)
			{
				//Debug.DrawRay(head.position, head.forward * hit.distance, Color.red);
				return true;
			}
			else
			{
				return false;
			}
		}
		return false;
	}

	SiphonTarget currentlyLookedAtSiphonTarget()
	{
		RaycastHit hit;
		if (Physics.Raycast(head.position, head.forward, out hit, maxExtendDistance, siphonLayerMask))
		{
			//Debug.Log(hit.transform.gameObject.layer);
			if (hit.transform.gameObject.GetComponent<SiphonTarget>() != null)
			{
				return hit.transform.gameObject.GetComponent<SiphonTarget>();
			}
			else
			{
				return null;
			}
		}
		return null;
	}

	public void NotSiphoning()
	{
		currentTimer = 0;
	}

	public int getSiphoningRate()
	{
		return currentLevel / currentLevelWithoutDamage;
	}

	public float getMaxSiphonDistance()
	{
		return currentLevel * maxSiphonDistanceMultiplier;
	}

	public void addDollars(float amount)
	{
		dollars += amount;
	}

	public void ToggleSiphon()
	{
		if (currentlyLookedAtSiphonTarget() != null && currentlyLookedAtSiphonTarget().siphoner != null)
		{
			//Debug.Log("retracting other siphon");
			if (isLookingAtSiphonTarget())
			{
				//Debug.Log("looking at valid target");
				if (currentTimer >= retractTime)
				{
					currentlyLookedAtSiphonTarget().notBeingSiphoned(this);
					currentTimer = 0;
					//Debug.Log("other Siphon Retracted");
				}
				else
				{
					currentTimer += Time.deltaTime;
					//Debug.Log("retracting other Siphon");
				}
			}
			else
			{
				//Debug.Log("no valid target");
			}
		}

		else if (extended)
		{
			//Debug.Log("extended");
			if (currentTimer >= retractTime)
			{
				siphonTarget.notBeingSiphoned(this);
				currentTimer = 0;
				//Debug.Log("Siphon Retracted");
			}
			else
			{
				currentTimer += Time.deltaTime;
				//Debug.Log("Retracting Siphon");
			}
		}
		else
		{
			//Debug.Log("retracted");
			if (isLookingAtSiphonTarget())
			{
				//Debug.Log("looking at valid target");
				if (currentTimer >= extendedTime)
				{
					currentlyLookedAtSiphonTarget().beingSiphoned(this);
					currentTimer = 0;
					//Debug.Log("Siphon Extended");
				}
				else
				{
					currentTimer += Time.deltaTime;
					//Debug.Log("Extending Siphon");
				}
			}
			else
			{
				//Debug.Log("no valid target");
			}
		}
	}
}
