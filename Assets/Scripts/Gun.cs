using System.Collections;
using System.Collections.Generic;
//using Lean.Pool;
using UnityEngine;
using UnityEngine.Pool;

public class Gun : MonoBehaviour
{
	public GunDataScriptableObject gunData;

	private Rigidbody weapon;
	private ParticleSystem shootSystem;
	private LineRenderer laser;
	private float lastRaycastTime;
	private float raycastInterval = 0.5f;
	private GameObject weaponHitPoint;
	private float lastShootTime;
	private GameObject weaponSlotLocation;
	private ObjectPool<TrailRenderer> TrailPool;
	private ObjectPool<ParticleSystem> HitParticlePool;
	public bool isPowered;
	private float chargeTimeLeftCache;
	private float prepTimeLeftCache;
	private Transform prepInd;
	private Vector3 prepIndicatorSizeCache;
	public bool isFiringBurst = false;
	public bool isFiring = false;


	public void SetParent(GameObject parent, Rigidbody weap)
	{
		weaponSlotLocation = parent;
		weapon = weap;
	}

	// Start is called before the first frame update
	void Start()
	{
		lastShootTime = 0;
		TrailPool = new ObjectPool<TrailRenderer>(CreateTrail);
		HitParticlePool = new ObjectPool<ParticleSystem>(CreateHitParticles);

		GameObject model = Instantiate(gunData.ModelPrefab);
		model.transform.SetParent(weaponSlotLocation.transform, false);
		model.transform.localPosition = gunData.SpawnPoint;
		model.transform.localRotation = Quaternion.Euler(gunData.SpawnRotation);

		shootSystem = model.GetComponentInChildren<ParticleSystem>();
		laser = model.GetComponentInChildren<LineRenderer>();


		prepTimeLeftCache = gunData.shootConfig.prepTime;
		prepInd = model.transform.Find("prep");
		prepIndicatorSizeCache = prepInd.transform.localScale;
		prepInd.gameObject.SetActive(false);

		raycastInterval += Random.Range(0.01f, 0.02f);
	}

	public bool isCharged()
	{
		return chargeTimeLeftCache <= 0;
	}

	public bool Shoot()
	{
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
				prepInd.gameObject.SetActive(false);
				prepInd.localScale = prepIndicatorSizeCache;

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
				yield return new WaitForSeconds(gunData.shootConfig.burst_delayBetweenShots);
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
		for (int i = 0; i < gunData.shootConfig.bulletsPerShot; i++)
		{
			//lastShootTime = Time.time;
			chargeTimeLeftCache = gunData.shootConfig.fireRate;
			shootSystem.Play();
			Vector3 shootDirection = shootSystem.transform.forward
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
			shootDirection.Normalize();
			weapon.AddForce((-weaponSlotLocation.GetComponentInParent<GunSelector>().gameObject.transform.right).normalized * gunData.shootConfig.recoil, ForceMode.Impulse);

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
				ManageHit(hit);
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
		return true;
	}

	private void ManageHit(RaycastHit hit)
	{
		Rigidbody hitRb = hit.collider.GetComponent<Rigidbody>();
		HeatContainer heatContainer = hit.collider.GetComponent<HeatContainer>();
		if (heatContainer == null)
		{
			heatContainer = hit.collider.GetComponentInParent<HeatContainer>();
		}
		LimbToSystemLinker limb = hit.collider.GetComponent<LimbToSystemLinker>();
		MarchingCubesGenerator marchingCubes = hit.collider.GetComponent<MarchingCubesGenerator>();
		BodyController bodyController = hit.collider.GetComponentInParent<BodyController>();
		BodyVFXController bodyVFXController = hit.collider.GetComponentInParent<BodyVFXController>();
		PracticeTarget practiceTarget = hit.collider.GetComponentInParent<PracticeTarget>();

		if (heatContainer != null)
		{
			heatContainer.IncreaseHeat(this, gunData.shootConfig.heatPerShot);
		}
		if (limb != null)
		{
			Vector3 impulse = shootSystem.transform.forward * gunData.shootConfig.impactForce;
			//Debug.Log("hit limb");
			DamageInfo damageInfo = new DamageInfo(gunData.shootConfig.heatPerShot);
			damageInfo.impactForce = gunData.shootConfig.impactForce;
			damageInfo.impactVector = impulse;
			limb.TakeDamage(damageInfo);
			if (limb.limb.specificLimb == Limb.LimbID.rightArm)
			{
				hitRb.AddForce(impulse * 0.75f, ForceMode.Impulse);
			}
			else
			{
				hitRb.AddForce(impulse * 2.5f, ForceMode.Impulse);
			}
			// StartCoroutine(
			// 		PlayHitParticles(hit));

		}
		else if (hitRb != null)
		{
			//Debug.Log("hit rb");
			Vector3 impulse = shootSystem.transform.forward * gunData.shootConfig.impactForce;
			hitRb.AddForce(impulse * 2.5f, ForceMode.Impulse);
		}
		if (marchingCubes != null)
		{
			marchingCubes.TakeDamage(hit.point, gunData.shootConfig.marchingCubesDamage);
		}
		if (bodyVFXController != null && bodyController != null)
		{
			// if (bodyController.cooling.isOverheated)
			// {
			bodyVFXController.doBloodParticles(hit.point, Quaternion.Euler(hit.normal));
			// }
			// TODO make these hit particles object pooled
			Destroy(Instantiate(gunData.shootConfig.hitParticles, hit.point, Quaternion.Euler(hit.normal)).gameObject, 1f);

		}
		if (practiceTarget != null)
		{
			Destroy(Instantiate(gunData.shootConfig.hitParticles, hit.point, Quaternion.Euler(hit.normal)).gameObject, 1f);
			practiceTarget.DestroyTarget();
		}
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
			remainingDistance -= gunData.trailConfig.SimulationSpeed * Time.deltaTime;

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

		yield return new WaitForSeconds(gunData.trailConfig.Duration);
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

		yield return new WaitForSeconds(gunData.trailConfig.Duration);
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
		//Debug.Log(chargeTimeLeftCache);
		if (chargeTimeLeftCache > 0)
		{
			chargeTimeLeftCache -= Time.deltaTime;
		}

		if (isPowered && prepTimeLeftCache > 0 && isFiring)
		{
			prepInd.gameObject.SetActive(true);
			prepInd.localScale *= 1.05f;

			prepTimeLeftCache -= Time.deltaTime;
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

}
