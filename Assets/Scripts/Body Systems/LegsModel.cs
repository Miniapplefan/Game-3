using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegsModel : SystemModel
{
	public Rigidbody rb;
	public Transform headOrientation;

	public float moveAcceleration = 800f;
	public float moveDeacceleration = 0.2f;
	private float moveDeaccelerationX;
	private float moveDeaccelerationZ;
	public float baseWalkSpeed = 4f;
	public float speedMultiplier = 1f;

	public float taggingModifier = 100f;

	public float taggingRecoveryRate = 0.1f;

	public float taggingRecoveryRateCache = 0.1f;

	public float taggingRecoveryRateRecoveryRate = 0.0002f;

	public bool canMove = true;

	private float tickTimer = 0f; // Timer for tracking the current tick.
	private bool isBurstSpeed = true; // Alternates between burst and slow speed.
	private float currentTickDuration = 0f; // Duration of the current tick.

	public float minLimpSpeed = 0.5f; // Minimum speed when limping.
	public float maxTickDuration = 0.5f; // Maximum duration of a slow tick.

	public ICommand forwardCommand;
	public ICommand backwardCommand;
	public ICommand leftCommand;
	public ICommand rightCommand;

	public LegsModel(int currentLvl, Rigidbody r, Transform head) :
	base(currentLvl)
	{
		rb = r;
		headOrientation = head;
		rightLegHealth = currentLvl;
		leftLegHealth = currentLvl;
		moveSpeed = getMoveSpeed();
	}

	protected override void InitCommands()
	{
		forwardCommand = new MoveForwardCommand(this);
		backwardCommand = new MoveBackwardCommand(this);
		leftCommand = new MoveLeftCommand(this);
		rightCommand = new MoveRightCommand(this);
	}

	public override void SetNameAndMaxLevel()
	{
		name = BodyInfo.systemID.Legs;
		maxLevel = 4;
	}

	public override void UpgradeLevel(int amount)
	{
		base.UpgradeLevel(amount);
		rightLegHealth = Mathf.Clamp(rightLegHealth + amount, currentLevelWithoutDamage, maxLevel);
		leftLegHealth = Mathf.Clamp(leftLegHealth + amount, currentLevelWithoutDamage, maxLevel); ;
	}

	float moveSpeed;

	public int rightLegHealth;
	public float rightLegCurrentHealth;
	public int leftLegHealth;
	public float leftLegCurrentHealth;


	float getSpeedFromLeg(int legHealth)
	{
		switch (legHealth)
		{
			case 0:
				return 0f;
			case 1:
				return 0.5f;
			case 2:
				return 0.6f;
			case 3:
				return 0.8f;
			case 4:
				return 1.0f;
			default:
				return 0f;
		}
	}

	public void UpdateMovementTick(float deltaTime)
	{
		tickTimer += deltaTime;

		// If the tick timer exceeds the current duration, toggle burst/slow and reset.
		if (tickTimer >= currentTickDuration)
		{
			isBurstSpeed = !isBurstSpeed;
			tickTimer = 0f;

			// Recalculate the tick duration based on leg health and tagging.
			UpdateTickDuration();
		}
	}

	private void UpdateTickDuration()
	{
		// Calculate tick duration based on leg health and tagging.
		float legHealthFactor = (getSpeedFromLeg(leftLegHealth) + getSpeedFromLeg(rightLegHealth)) / 2f;
		currentTickDuration = legHealthFactor >= 1 && taggingModifier >= 100 ? 0 : Mathf.Lerp(0f, maxTickDuration, 1f - legHealthFactor + getTagging()); //* (100f / taggingModifier)
	}

	public float getMoveSpeed()
	{
		// Return different speeds based on whether it's a burst or slow tick.
		if (!canMove)
			return 0f;

		float baseSpeed = (getSpeedFromLeg(rightLegHealth) + getSpeedFromLeg(leftLegHealth));
		float limpModifier = getTagging();

		return isBurstSpeed ? baseSpeed : baseSpeed * limpModifier;
	}

	public float getBaseSpeed()
	{
		return getSpeedFromLeg(rightLegHealth) + getSpeedFromLeg(leftLegHealth);
	}



	// public float getMoveSpeed()
	// {
	// 	if (canMove)
	// 	{
	// 		return (getSpeedFromLeg(rightLegHealth) + getSpeedFromLeg(leftLegHealth)) * (taggingModifier / 100f);
	// 	}
	// 	else
	// 	{
	// 		return 0f;
	// 	}
	// }

	public void RecoverFromTagging(float heatLevel)
	{
		if (taggingModifier < 100f)
		{
			taggingModifier += taggingRecoveryRateCache * heatLevel;
		}
		if (taggingRecoveryRateCache < taggingRecoveryRate)
		{
			taggingRecoveryRateCache += taggingRecoveryRateRecoveryRate;
		}
	}

	public float getTagging()
	{
		return taggingModifier / 100f;
	}

	public void HandleTagging(Limb l, float impact)
	{
		float taggingDam = impact / 10;
		switch (l.specificLimb)
		{
			case Limb.LimbID.leftLeg:
				DealTagging(taggingDam, 0.02f);
				break;
			case Limb.LimbID.rightLeg:
				DealTagging(taggingDam, 0.02f);
				break;
			case Limb.LimbID.torso:
				DealTagging(taggingDam, 0.03f);
				break;
			case Limb.LimbID.head:
				DealTagging(taggingDam / 10, 0.06f);
				break;
			default:
				break;
		}
	}

	private void DealTagging(float tagAmount, float taggingRecoveryAmount)
	{
		taggingModifier = Mathf.Max(10f, taggingModifier - tagAmount);
		taggingRecoveryRateCache = Mathf.Max(0.02f, taggingRecoveryRateCache - taggingRecoveryAmount);
	}

	public void damageLeftLeg(int amount)
	{
		leftLegHealth = Mathf.Clamp(leftLegHealth - amount, 0, leftLegHealth);
	}

	public void damangeLeftLegCurrentHealth(float amount)
	{
		leftLegCurrentHealth = Mathf.Clamp(leftLegCurrentHealth - amount, 0, leftLegCurrentHealth);
		if (rightLegCurrentHealth <= 0)
		{
			damageLeftLeg(leftLegHealth);
		}
	}

	public void damageRightLeg(int amount)
	{
		rightLegHealth = Mathf.Clamp(rightLegHealth - amount, 0, rightLegHealth);
	}

	public void damangeRightLegCurrentHealth(float amount)
	{
		rightLegCurrentHealth = Mathf.Clamp(rightLegCurrentHealth - amount, 0, rightLegCurrentHealth);
		if (rightLegCurrentHealth <= 0)
		{
			damageRightLeg(rightLegHealth);
		}
	}

	public void healLeftLeg(int amount)
	{
		leftLegHealth = Mathf.Clamp(leftLegHealth + amount, leftLegHealth, currentLevelWithoutDamage);
	}

	public void healRightLeg(int amount)
	{
		rightLegHealth = Mathf.Clamp(rightLegHealth + amount, rightLegHealth, currentLevelWithoutDamage);
	}

	public bool isCurrentVelocityLessThanMax()
	{
		Vector2 horizontalMovement = new Vector2(rb.velocity.x, rb.velocity.z);
		return horizontalMovement.magnitude < baseWalkSpeed * getMoveSpeed() * speedMultiplier;
	}

	public void DoMoveDeacceleration()
	{
		float wdx = Mathf.SmoothDamp(rb.velocity.x, 0, ref moveDeaccelerationX, moveDeacceleration);
		float wdz = Mathf.SmoothDamp(rb.velocity.z, 0, ref moveDeaccelerationZ, moveDeacceleration);
		rb.velocity = new Vector3(wdx, rb.velocity.y, wdz);
	}

	public void OnCoolingSystemOverheat()
	{
		canMove = false;
	}

	public void OnCoolingSystemCooledOff()
	{
		canMove = true;
		taggingModifier = 100;
		taggingRecoveryRateCache = taggingRecoveryRate;
	}

	#region Execute Commands

	public void ExecuteForward()
	{
		if (canMove)
		{
			forwardCommand.Execute();

		}
	}

	public void ExecuteBackward()
	{
		if (canMove)
		{
			backwardCommand.Execute();
		}
	}

	public void ExecuteLeft()
	{
		if (canMove)
		{
			leftCommand.Execute();
		}
	}

	public void ExecuteRight()
	{
		if (canMove)
		{
			rightCommand.Execute();
		}
	}

	#endregion
}
