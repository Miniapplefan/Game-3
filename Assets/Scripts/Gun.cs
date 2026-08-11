using System.Collections;
using System.Collections.Generic;
//using Lean.Pool;
using UnityEngine;
using UnityEngine.Pool;

public readonly struct EnemyHitByPlayerInfo
{
	public BodyController Target { get; }
	public Vector3 ImpactPosition { get; }
	public bool WasKillingBlow { get; }

	public EnemyHitByPlayerInfo(BodyController target, Vector3 impactPosition, bool wasKillingBlow)
	{
		Target = target;
		ImpactPosition = impactPosition;
		WasKillingBlow = wasKillingBlow;
	}
}

public readonly struct GunReloadAudioInfo
{
	public float DelaySeconds { get; }

	public GunReloadAudioInfo(float delaySeconds)
	{
		DelaySeconds = Mathf.Max(0f, delaySeconds);
	}
}

public class Gun : MonoBehaviour
{
	public GunDataScriptableObject gunData;
	public Transform ModelRoot => modelRoot;
	public Transform GripTransform => mountPoints != null ? mountPoints.Grip : null;
	public Transform MuzzleTransform => mountPoints != null && mountPoints.Muzzle != null ? mountPoints.Muzzle : shootSystem != null ? shootSystem.transform : null;
	public bool IsAIControlled => isAI;
	public bool CanStartReload => !isReloading
		&& gunData != null
		&& gunData.shootConfig != null
		&& currentShotsInMag < gunData.shootConfig.magSize;
	public event System.Action<Gun> ActualShotFired;
	public event System.Action<Gun> EmptyTriggerPulled;
	public event System.Action<Gun, EnemyHitByPlayerInfo> EnemyHitByPlayer;
	public event System.Action<Gun, GunReloadAudioInfo> ReloadStarted;
	public event System.Action<Gun, GunReloadAudioInfo> ReloadCompleted;

	private Rigidbody weapon;
	private Transform modelRoot;
	private WeaponMountPoints mountPoints;
	private ParticleSystem shootSystem;
	private LineRenderer laser;
	private float lastRaycastTime;
	private float raycastInterval = 0.5f;
	private GameObject weaponHitPoint;
	private float lastShootTime;
	private Transform weaponSlotLocation;
	private ObjectPool<TrailRenderer> TrailPool;
	private ObjectPool<ParticleSystem> HitParticlePool;
	public bool isPowered;
	public int currentShotsInMag;
	public bool isReloading = false;
	public float reloadTimeCache;
	private float reloadAudioDelaySeconds;
	private bool reloadCompletionUsesAudioTime;
	private float chargeTimeLeftCache;
	private float prepTimeLeftCache;
	private Transform prepInd;
	private Vector3 prepIndicatorSizeCache;
	public bool isFiringBurst = false;
	public bool isFiring = false;
	bool isAI;
	private bool hasNpcBallisticAimPoint;
	private Vector3 npcBallisticAimPoint;
	private BodyController bodyController;
	private ActiveRagdollController activeRagdollController;


	public void SetParent(GameObject parent, Rigidbody weap)
	{
		weaponSlotLocation = parent != null ? parent.transform : null;
		weapon = weap;
	}

	// Start is called before the first frame update
	void Start()
	{
		lastShootTime = 0;
		TrailPool = new ObjectPool<TrailRenderer>(CreateTrail);
		HitParticlePool = new ObjectPool<ParticleSystem>(CreateHitParticles);

		GameObject model = Instantiate(gunData.ModelPrefab);
		modelRoot = model.transform;
		model.AddComponent<GunAimOwner>().Init(this);
		mountPoints = model.GetComponent<WeaponMountPoints>();
		if (mountPoints == null)
		{
			mountPoints = model.GetComponentInChildren<WeaponMountPoints>();
		}

		AttachModelToSocket(modelRoot);

		shootSystem = model.GetComponentInChildren<ParticleSystem>();
		laser = model.GetComponentInChildren<LineRenderer>();

		currentShotsInMag = gunData.shootConfig.magSize;
		reloadTimeCache = gunData.shootConfig.reloadTime;

		prepTimeLeftCache = gunData.shootConfig.prepTime;
		prepInd = model.transform.Find("prep");
		if (prepInd != null)
		{
			prepIndicatorSizeCache = prepInd.transform.localScale;
			prepInd.gameObject.SetActive(false);
		}

		raycastInterval += Random.Range(0.01f, 0.02f);

		isAI = weapon.GetComponentInParent<AIController>() != null ? true : false;
		GunAudioEmitter audioEmitter = GetComponent<GunAudioEmitter>();
		if (audioEmitter == null)
		{
			audioEmitter = gameObject.AddComponent<GunAudioEmitter>();
		}
		audioEmitter.Initialize(this, isAI);

		if (!isAI)
		{
			bodyController = weapon.GetComponentInParent<BodyController>();
			if (bodyController != null)
			{
				activeRagdollController = bodyController.GetComponentInChildren<ActiveRagdollController>();
			}
		}
	}

	private void AttachModelToSocket(Transform model)
	{
		if (model == null || weaponSlotLocation == null)
		{
			return;
		}

		if (mountPoints != null && mountPoints.Grip != null)
		{
			Transform grip = mountPoints.Grip;
			Quaternion rotationDelta = weaponSlotLocation.rotation * Quaternion.Inverse(grip.rotation);
			Quaternion targetRotation = rotationDelta * model.rotation;
			Vector3 targetPosition = weaponSlotLocation.position + rotationDelta * (model.position - grip.position);

			model.SetPositionAndRotation(targetPosition, targetRotation);
			model.SetParent(weaponSlotLocation, true);
			return;
		}

		model.SetParent(weaponSlotLocation, false);
		model.localPosition = gunData.SpawnPoint;
		model.localRotation = Quaternion.Euler(gunData.SpawnRotation);
	}

	public bool isCharged()
	{
		return chargeTimeLeftCache <= 0;
	}

	public void SetNpcBallisticAimPoint(Vector3 point)
	{
		hasNpcBallisticAimPoint = true;
		npcBallisticAimPoint = point;
	}

	public void ClearNpcBallisticAimPoint()
	{
		hasNpcBallisticAimPoint = false;
		npcBallisticAimPoint = Vector3.zero;
	}

	public bool Shoot(bool triggerPressedThisFrame = false)
	{
		if (triggerPressedThisFrame && !isAI && !isReloading && currentShotsInMag <= 0)
		{
			EmptyTriggerPulled?.Invoke(this);
		}

		if (chargeTimeLeftCache <= 0)
		{
			isFiring = true;
			if (prepTimeLeftCache <= 0)
			{
				// Debug.Log("Done charging");
				if (gunData.shootConfig.isBurst)
				{
					StartCoroutine(ShootBurst());
				}
				else
				{
					SingleShot();
				}

				chargeTimeLeftCache = gunData.shootConfig.fireRate;
				prepTimeLeftCache = gunData.shootConfig.prepTime;
				if (prepInd != null)
				{
					prepInd.gameObject.SetActive(false);
					prepInd.localScale = prepIndicatorSizeCache;
				}

				isFiring = false;
				return true;
			}
			else
			{
				return true;
			}
		}
		else
		{
			return false;
		}
	}

	private IEnumerator ShootBurst()
	{
		for (int i = 0; i < gunData.shootConfig.burst_numShots; i++)
		{
			isFiringBurst = true;
			if (SingleShot())
			{
				yield return WaitForWeaponSeconds(gunData.shootConfig.burst_delayBetweenShots);
			}
			else
			{
				break;
			}
		}
		isFiringBurst = false;
	}

	public bool SingleShot()
	{

		if (isReloading) return false;

		if (!isAI)
		{
			if (currentShotsInMag > 0)
			{
				currentShotsInMag--;
			}
			else
			{
				return false;
			}
		}

		ApplyMovementAimShotImpulse();
		Dictionary<BodyController, Vector3> enemiesHitThisShot = null;

		for (int i = 0; i < gunData.shootConfig.bulletsPerShot; i++)
		{
			//lastShootTime = Time.time;
			chargeTimeLeftCache = gunData.shootConfig.fireRate;
			shootSystem.Play();
			Vector3 shotCenterDirection = GetNpcShotCenterDirection();
			Vector3 shootDirection = GetSpreadDirection(shotCenterDirection);
			if (isAI)
			{
				shootDirection = ClampDirectionOutsideNpcInnerCone(shotCenterDirection, shootDirection);
				shootDirection = ClampDirectionOutsideNpcExcludedLowerArc(shotCenterDirection, shootDirection);
			}
			Vector3 recoilDirection = modelRoot != null ? modelRoot.up.normalized : transform.up.normalized;
			weapon.AddForce(recoilDirection * gunData.shootConfig.recoil, ForceMode.Impulse);

			if (isAI)
			{
				//				Debug.Log("FIRING AI BULLET");
				Instantiate(gunData.npcBulletPrefab, shootSystem.transform.position, Quaternion.LookRotation(shootDirection));
			}
			else
			{
				if (Physics.Raycast(
						shootSystem.transform.position,
						shootDirection,
						out RaycastHit hit,
						gunData.shootConfig.maxRange,
						gunData.shootConfig.HitMask
					))
				{
					StartCoroutine(
						PlayTrail(
							shootSystem.transform.position,
							hit.point,
							hit
						)
					);
					BodyController damagedEnemy = ManageHit(hit);
					if (damagedEnemy != null)
					{
						if (enemiesHitThisShot == null)
						{
							enemiesHitThisShot = new Dictionary<BodyController, Vector3>();
						}

						if (!enemiesHitThisShot.ContainsKey(damagedEnemy))
						{
							enemiesHitThisShot.Add(damagedEnemy, hit.point);
						}
					}
				}
				else
				{
					StartCoroutine(
						PlayTrail(
							shootSystem.transform.position,
							shootSystem.transform.position + (shootDirection * gunData.shootConfig.maxRange),
							new RaycastHit()
						)
					);
				}
			}
		}
		ActualShotFired?.Invoke(this);
		NotifyEnemiesHitByPlayer(enemiesHitThisShot);
		return true;
	}

	private void NotifyEnemiesHitByPlayer(Dictionary<BodyController, Vector3> enemiesHitThisShot)
	{
		if (enemiesHitThisShot == null)
		{
			return;
		}

		foreach (KeyValuePair<BodyController, Vector3> enemyHit in enemiesHitThisShot)
		{
			BodyController target = enemyHit.Key;
			if (target == null)
			{
				continue;
			}

			EnemyHitByPlayerInfo hitInfo = new EnemyHitByPlayerInfo(
				target,
				enemyHit.Value,
				target.isDead
			);
			EnemyHitByPlayer?.Invoke(this, hitInfo);
		}
	}

	private Vector3 GetNpcShotCenterDirection()
	{
		if (shootSystem == null)
		{
			return transform.forward;
		}

		if (!isAI || !hasNpcBallisticAimPoint)
		{
			return shootSystem.transform.forward;
		}

		Vector3 direction = npcBallisticAimPoint - shootSystem.transform.position;
		return direction.sqrMagnitude > 0.0001f
			? direction.normalized
			: shootSystem.transform.forward;
	}

	private void ApplyMovementAimShotImpulse()
	{
		if (isAI)
		{
			return;
		}

		GetOwnerBodyController();

		if (activeRagdollController == null && bodyController != null)
		{
			activeRagdollController = bodyController.GetComponentInChildren<ActiveRagdollController>();
		}

		if (bodyController == null || activeRagdollController == null)
		{
			return;
		}

		if (bodyController.gunsL != null && bodyController.gunsL.ActiveGun1 == this)
		{
			activeRagdollController.ApplyMovementAimShotImpulse(true);
			return;
		}

		if (bodyController.guns != null && bodyController.guns.ActiveGun1 == this)
		{
			activeRagdollController.ApplyMovementAimShotImpulse(false);
		}
	}

	private BodyController GetOwnerBodyController()
	{
		if (bodyController == null && weapon != null)
		{
			bodyController = weapon.GetComponentInParent<BodyController>();
		}

		return bodyController;
	}

	private Vector3 GetSpreadDirection(Vector3 forward)
	{
		Vector3 shootDirection = forward
			+ new Vector3(
				Random.Range(
					-gunData.shootConfig.Spread.x,
					gunData.shootConfig.Spread.x
				),
				Random.Range(
					-gunData.shootConfig.Spread.y,
					gunData.shootConfig.Spread.y
				),
				Random.Range(
					-gunData.shootConfig.Spread.z,
					gunData.shootConfig.Spread.z
				)
			);

		if (shootDirection.sqrMagnitude <= Mathf.Epsilon)
		{
			return forward.normalized;
		}

		return shootDirection.normalized;
	}

	private Vector3 ClampDirectionOutsideNpcInnerCone(Vector3 centerForward, Vector3 shootDirection)
	{
		float innerConeAngle = GetDistanceCorrectedNpcInnerConeAngle(centerForward);
		if (innerConeAngle <= 0f)
		{
			return shootDirection.normalized;
		}

		Vector3 center = centerForward.normalized;
		Vector3 direction = shootDirection.normalized;
		if (Vector3.Angle(center, direction) >= innerConeAngle)
		{
			return direction;
		}

		Vector3 radial = Vector3.ProjectOnPlane(direction, center);
		if (radial.sqrMagnitude <= 0.0001f)
		{
			radial = GetRandomPerpendicular(center);
		}
		else
		{
			radial.Normalize();
		}

		float angleRadians = innerConeAngle * Mathf.Deg2Rad;
		return (center * Mathf.Cos(angleRadians) + radial * Mathf.Sin(angleRadians)).normalized;
	}

	private float GetDistanceCorrectedNpcInnerConeAngle(Vector3 centerForward)
	{
		float innerConeRadius = gunData.shootConfig.npcInnerConeRadius;
		if (innerConeRadius <= 0f || !TryGetNpcBodyDistance(centerForward, out float bodyDistance))
		{
			return gunData.shootConfig.npcInnerConeAngle;
		}

		return Mathf.Atan(innerConeRadius / bodyDistance) * Mathf.Rad2Deg;
	}

	private bool TryGetNpcBodyDistance(Vector3 centerForward, out float bodyDistance)
	{
		bodyDistance = 0f;
		const int bodyLayer = 6;
		RaycastHit[] hits = Physics.RaycastAll(
			shootSystem.transform.position,
			centerForward.normalized,
			Mathf.Infinity,
			1 << bodyLayer,
			QueryTriggerInteraction.Ignore
		);
		if (hits.Length == 0)
		{
			return false;
		}

		Transform ownRoot = transform.root;
		float closestDistance = float.PositiveInfinity;
		for (int i = 0; i < hits.Length; i++)
		{
			RaycastHit hit = hits[i];
			if (hit.collider == null || hit.collider.transform.root == ownRoot)
			{
				continue;
			}

			if (hit.distance < closestDistance)
			{
				closestDistance = hit.distance;
			}
		}

		if (float.IsPositiveInfinity(closestDistance) || closestDistance <= Mathf.Epsilon)
		{
			return false;
		}

		bodyDistance = closestDistance;
		return true;
	}

	private Vector3 ClampDirectionOutsideNpcExcludedLowerArc(Vector3 centerForward, Vector3 shootDirection)
	{
		float excludedArcDegrees = gunData.shootConfig.npcExcludedLowerArcDegrees;
		if (excludedArcDegrees <= 0f)
		{
			return shootDirection.normalized;
		}

		Vector3 center = centerForward.normalized;
		Vector3 direction = shootDirection.normalized;
		float coneAngle = Vector3.Angle(center, direction);
		Vector3 radial = Vector3.ProjectOnPlane(direction, center);
		if (radial.sqrMagnitude <= 0.0001f)
		{
			radial = GetRandomPerpendicular(center);
		}
		else
		{
			radial.Normalize();
		}

		Vector3 bottomRadial = Vector3.ProjectOnPlane(Vector3.down, center);
		if (bottomRadial.sqrMagnitude <= 0.0001f)
		{
			bottomRadial = GetRandomPerpendicular(center);
		}
		else
		{
			bottomRadial.Normalize();
		}

		float signedAngleFromBottom = Vector3.SignedAngle(bottomRadial, radial, center);
		if (Mathf.Abs(signedAngleFromBottom) >= excludedArcDegrees)
		{
			return direction;
		}

		float side = Mathf.Approximately(signedAngleFromBottom, 0f)
			? (Random.value < 0.5f ? -1f : 1f)
			: Mathf.Sign(signedAngleFromBottom);
		Vector3 clampedRadial = Quaternion.AngleAxis(side * excludedArcDegrees, center) * bottomRadial;
		float coneAngleRadians = coneAngle * Mathf.Deg2Rad;

		return (center * Mathf.Cos(coneAngleRadians) + clampedRadial * Mathf.Sin(coneAngleRadians)).normalized;
	}

	private Vector3 GetRandomPerpendicular(Vector3 axis)
	{
		Vector3 radial = Vector3.ProjectOnPlane(Random.onUnitSphere, axis);
		if (radial.sqrMagnitude <= 0.0001f)
		{
			radial = Vector3.ProjectOnPlane(Vector3.up, axis);
		}
		if (radial.sqrMagnitude <= 0.0001f)
		{
			radial = Vector3.ProjectOnPlane(Vector3.right, axis);
		}

		return radial.normalized;
	}

	public bool StartReload(float audioDelaySeconds = 0f)
	{
		if (!CanStartReload)
		{
			return false;
		}

		isReloading = true;
		reloadAudioDelaySeconds = Mathf.Max(0f, audioDelaySeconds);
		reloadCompletionUsesAudioTime = false;
		reloadTimeCache = gunData.shootConfig.reloadTime + reloadAudioDelaySeconds;
		Debug.Log("started reload");
		ReloadStarted?.Invoke(this, new GunReloadAudioInfo(reloadAudioDelaySeconds));
		return true;
	}

	internal bool AlignReloadCompletionToAudio(float playbackDurationSeconds)
	{
		if (!isReloading || playbackDurationSeconds <= 0f)
		{
			return false;
		}

		reloadTimeCache = playbackDurationSeconds;
		reloadCompletionUsesAudioTime = true;
		return true;
	}

	private void ProcessReload()
	{
		if (reloadTimeCache > 0)
		{
			reloadTimeCache -= reloadCompletionUsesAudioTime
				? Time.unscaledDeltaTime
				: GetWeaponDeltaTime();
		}

		if (reloadTimeCache > 0)
		{
			return;
		}

		Debug.Log("finished reload");
		isReloading = false;
		currentShotsInMag = gunData.shootConfig.magSize;
		float audioDelaySeconds = reloadAudioDelaySeconds;
		reloadAudioDelaySeconds = 0f;
		reloadCompletionUsesAudioTime = false;
		ReloadCompleted?.Invoke(this, new GunReloadAudioInfo(audioDelaySeconds));
	}

	private BodyController ManageHit(RaycastHit hit)
	{
		BodyController damagedEnemy = null;
		Rigidbody hitRb = hit.collider.GetComponent<Rigidbody>();
		HeatContainer heatContainer = hit.collider.GetComponent<HeatContainer>();
		if (heatContainer == null)
		{
			heatContainer = hit.collider.GetComponentInParent<HeatContainer>();
		}
		LimbToSystemLinker limb = hit.collider.GetComponent<LimbToSystemLinker>();
		MarchingCubesGenerator marchingCubes = hit.collider.GetComponent<MarchingCubesGenerator>();
		BodyController targetBodyController = hit.collider.GetComponentInParent<BodyController>();
		if (targetBodyController == null && hit.collider.transform.root != null)
		{
			targetBodyController = hit.collider.transform.root.GetComponentInChildren<BodyController>(true);
		}

		BodyVFXController bodyVFXController = hit.collider.GetComponentInParent<BodyVFXController>();
		if (bodyVFXController == null && hit.collider.transform.root != null)
		{
			bodyVFXController = hit.collider.transform.root.GetComponentInChildren<BodyVFXController>(true);
		}

		PracticeTarget practiceTarget = hit.collider.GetComponentInParent<PracticeTarget>();
		if (practiceTarget == null && hit.collider.transform.root != null)
		{
			practiceTarget = hit.collider.transform.root.GetComponentInChildren<PracticeTarget>(true);
		}

		if (heatContainer != null)
		{
			heatContainer.IncreaseHeat(this, gunData.shootConfig.heatPerShot);
		}
		if (limb != null)
		{
			Vector3 impulse = shootSystem.transform.forward * gunData.shootConfig.impactForce;
			float hitReactionScale = GetHitReactionScale(targetBodyController);
			//Debug.Log("hit limb");
			BodyController sourceBodyController = GetOwnerBodyController();
			bool wasLivingEnemyHitByPlayer = targetBodyController != null
				&& targetBodyController.isAI
				&& !targetBodyController.isDead
				&& sourceBodyController != null
				&& !sourceBodyController.isAI;
			float damageAmount = gunData.shootConfig.rawDamage;
			if (sourceBodyController != null && !sourceBodyController.isAI)
			{
				damageAmount *= sourceBodyController.getAuraDamageMultiplier();
			}

			DamageInfo damageInfo = new DamageInfo(damageAmount);
			damageInfo.impactForce = gunData.shootConfig.impactForce;
			damageInfo.impactVector = impulse;
			damageInfo.sourceBodyController = sourceBodyController;
			limb.TakeDamage(damageInfo);
			if (wasLivingEnemyHitByPlayer)
			{
				damagedEnemy = targetBodyController;
			}
			if (limb.limb.specificLimb == Limb.LimbID.rightArm)
			{
				hitRb.AddForce(impulse * 0.75f * hitReactionScale, ForceMode.Impulse);
			}
			else
			{
				hitRb.AddForce(impulse * 5f * hitReactionScale, ForceMode.Impulse);
			}
			// StartCoroutine(
			// 		PlayHitParticles(hit));

		}
		else if (hitRb != null)
		{
			//Debug.Log("hit rb");
			Vector3 impulse = shootSystem.transform.forward * gunData.shootConfig.impactForce;
			hitRb.AddForce(impulse * 2.5f * GetHitReactionScale(targetBodyController), ForceMode.Impulse);
		}
		if (marchingCubes != null)
		{
			marchingCubes.TakeDamage(hit.point, gunData.shootConfig.marchingCubesDamage);
		}
		if (bodyVFXController != null && targetBodyController != null)
		{
			// if (targetBodyController.cooling.isOverheated)
			// {
			bodyVFXController.doBloodParticles(hit.point, Quaternion.Euler(hit.normal));
			// }
			// TODO make these hit particles object pooled
			Destroy(Instantiate(gunData.shootConfig.hitParticles, hit.point, Quaternion.Euler(hit.normal)).gameObject, 1f);

		}
		if (practiceTarget != null && targetBodyController == null)
		{
			Destroy(Instantiate(gunData.shootConfig.hitParticles, hit.point, Quaternion.Euler(hit.normal)).gameObject, 1f);
			practiceTarget.DestroyTarget();
		}

		return damagedEnemy;
	}

	private IEnumerator PlayTrail(Vector3 StartPoint, Vector3 EndPoint, RaycastHit Hit)
	{
		TrailRenderer instance = TrailPool.Get();
		instance.gameObject.SetActive(true);
		instance.transform.position = StartPoint;
		// TrailRenderer instance = LeanPool.Spawn(CreateTrail());
		// instance.transform.position = StartPoint;
		yield return null; // avoid position carry-over from last frame if reused

		instance.emitting = true;

		float distance = Vector3.Distance(StartPoint, EndPoint);
		float remainingDistance = distance;
		while (remainingDistance > 0)
		{
			instance.transform.position = Vector3.Lerp(
				StartPoint,
				EndPoint,
				Mathf.Clamp01(1 - (remainingDistance / distance))
			);
			remainingDistance -= gunData.trailConfig.SimulationSpeed * GetWeaponDeltaTime();

			yield return null;
		}

		instance.transform.position = EndPoint;

		if (Hit.collider != null)
		{
			//SurfaceManager.Instance.HandleImpact(
			//    Hit.transform.gameObject,
			//    EndPoint,
			//    Hit.normal,
			//    ImpactType,
			//    0
			//);
		}

		yield return WaitForWeaponSeconds(gunData.trailConfig.Duration);
		yield return null;
		instance.emitting = false;
		instance.gameObject.SetActive(false);
		TrailPool.Release(instance);
		//LeanPool.Despawn(instance);
	}

	private IEnumerator PlayHitParticles(RaycastHit hit)
	{
		ParticleSystem instance = HitParticlePool.Get();
		instance.gameObject.SetActive(true);
		instance.transform.position = hit.point;
		instance.transform.rotation = Quaternion.Euler(hit.normal);

		yield return null;
		instance.Play();

		yield return WaitForWeaponSeconds(gunData.trailConfig.Duration);
		yield return null;
		instance.gameObject.SetActive(false);
		HitParticlePool.Release(instance);
	}

	private TrailRenderer CreateTrail()
	{
		GameObject instance = new GameObject("Bullet Trail");
		TrailRenderer trail = instance.AddComponent<TrailRenderer>();
		trail.colorGradient = gunData.trailConfig.Color;
		trail.material = gunData.trailConfig.Material;
		trail.widthCurve = gunData.trailConfig.WidthCurve;
		trail.time = gunData.trailConfig.Duration;
		trail.minVertexDistance = gunData.trailConfig.MinVertexDistance;

		trail.emitting = false;
		trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

		return trail;
	}

	private ParticleSystem CreateHitParticles()
	{
		GameObject instance = new GameObject("Hit Particles");
		ParticleSystem hitParticles = instance.AddComponent<ParticleSystem>();
		hitParticles = gunData.shootConfig.hitParticles;

		return hitParticles;
	}

	private void DrawLaser(Vector3 startPosition, Vector3 endPosition)
	{
		laser.SetPosition(0, startPosition);
		laser.SetPosition(1, endPosition);
	}

	private void Update()
	{

		if (isReloading) ProcessReload();

		//Debug.Log(chargeTimeLeftCache);
		if (chargeTimeLeftCache > 0)
		{
			chargeTimeLeftCache -= GetWeaponDeltaTime();
		}

		if (isPowered && prepTimeLeftCache > 0 && isFiring)
		{
			if (prepInd != null)
			{
				prepInd.gameObject.SetActive(true);
				prepInd.localScale *= 1.05f;
			}

			prepTimeLeftCache -= GetWeaponDeltaTime();
			Shoot();
		}

		if (isCharged() && Time.time - lastRaycastTime >= raycastInterval)
		{
			// if (Physics.Raycast(
			// 		shootSystem.transform.position,
			// 		shootSystem.transform.forward,
			// 		out RaycastHit hit,
			// 		gunData.shootConfig.maxRange,
			// 		gunData.shootConfig.HitMask
			// 	))
			// {
			// 	DrawLaser(shootSystem.transform.position, shootSystem.transform.position + shootSystem.transform.forward * Vector3.Distance(shootSystem.transform.position, hit.point));
			// }

		}
		else
		{
			DrawLaser(shootSystem.transform.position, shootSystem.transform.position);
		}
	}

	private BulletTimeChannel GetFireRateChannel()
	{
		return isAI ? BulletTimeChannel.EnemyFireRate : BulletTimeChannel.PlayerFireRate;
	}

	private float GetWeaponDeltaTime()
	{
		return BulletTimeManager.GetDeltaTime(GetFireRateChannel());
	}

	private IEnumerator WaitForWeaponSeconds(float seconds)
	{
		float elapsed = 0f;
		float targetSeconds = Mathf.Max(0f, seconds);
		while (elapsed < targetSeconds)
		{
			elapsed += GetWeaponDeltaTime();
			yield return null;
		}
	}

	private float GetHitReactionScale(BodyController targetBodyController)
	{
		if (targetBodyController == null)
		{
			return 1f;
		}

		return BulletTimeManager.GetScale(targetBodyController.isAI
			? BulletTimeChannel.EnemyHitReaction
			: BulletTimeChannel.PlayerActiveRagdoll);
	}

}

public class GunAimOwner : MonoBehaviour
{
	public Gun Gun { get; private set; }

	public void Init(Gun gun)
	{
		Gun = gun;
	}
}
