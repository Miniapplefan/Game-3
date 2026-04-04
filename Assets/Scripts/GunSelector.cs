using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class GunSelector : MonoBehaviour
{
	private const int ReloadBarWidth = 6;
	private static readonly Quaternion AmmoIndicatorFacingOffset = Quaternion.Euler(0f, 180f, 0f);
	private static readonly string[] ReloadIndicatorTexts = BuildReloadIndicatorTexts();

	[SerializeField]
	private GunType Gun1;
	[SerializeField]
	private GameObject Gun1Parent;
	[SerializeField]
	private GunType Gun2;
	[SerializeField]
	private GameObject Gun2Parent;
	[SerializeField]
	private GunType Gun3;
	[SerializeField]
	private GameObject Gun3Parent;
	[SerializeField]
	private List<GunDataScriptableObject> Guns;
	[SerializeField]
	private Rigidbody weapon;
	private LineRenderer laser;
	public TMP_Text ammoIndicator;
	private Vector3 raycastPoint;
	private Quaternion initialLocalRotation;
	private BodyController bodyController;
	private Transform ammoIndicatorHeadTarget;
	private bool hasAmmoIndicatorState;
	private bool lastAmmoIndicatorReloading;
	private int lastDisplayedAmmoCount;
	private int lastDisplayedReloadBarCount = -1;

	[Space]
	[Header("Runtime Filled")]
	public Gun ActiveGun1;
	public Gun ActiveGun2;
	public Gun ActiveGun3;
	bool isAI;
	public LayerMask raycastLayerMask;
	private float lastRaycastTime;
	private float raycastInterval = 0.5f; // Adjust this value as needed

	public Transform[] gunHolders;

	private void Start()
	{
		initialLocalRotation = transform.localRotation;
		ActiveGun1 = CreateGun(Gun1, Gun1Parent);
		ActiveGun2 = CreateGun(Gun2, Gun2Parent);
		ActiveGun3 = CreateGun(Gun3, Gun3Parent);
		Debug.Log("created guns");
		laser = GetComponent<LineRenderer>();
		isAI = GetComponentInParent<AIController>() != null ? true : false;
		bodyController = GetComponentInParent<BodyController>();
		ammoIndicatorHeadTarget = ResolveAmmoIndicatorHeadTarget();
	}

	void Update()
	{
		if (Time.time - lastRaycastTime >= raycastInterval)
		{
			PerformRaycast();
			RotateAmmoIndicatorTowardHead();
			lastRaycastTime = Time.time;
		}
	}

	void FixedUpdate()
	{
		float dist = Vector3.Distance(transform.position, raycastPoint);
		if (!isAI)
		{
			DrawLaser(transform.position + transform.forward * dist / 1.5f, transform.position + transform.forward * dist);
		}
		UpdateAmmoIndicator();
	}

	private void LateUpdate()
	{
	}

	void PerformRaycast()
	{
		RaycastHit hit;
		if (Physics.Raycast(transform.position, transform.forward, out hit, Mathf.Infinity, raycastLayerMask))
		{
			raycastPoint = hit.point;
			// Rotate the guns to look at the hit point
			RotateGuns(hit.point);
		}
		else
		{
			// No hit, keep the previous rotation
		}
	}

	void RotateGuns(Vector3 targetPoint)
	{
		foreach (Transform gun in gunHolders)
		{
			// Slot 1 is now treated as the hand-held weapon, so let the hand aim it.
			if (
				Gun1Parent != null
				&& (gun == Gun1Parent.transform || gun.IsChildOf(Gun1Parent.transform))
				&& ActiveGun1 != null
				&& ActiveGun1.GripTransform != null
			)
			{
				continue;
			}

			Vector3 targetDirection = targetPoint - gun.position;
			Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
			gun.rotation = targetRotation;
		}
	}

	public Quaternion GetPrimaryHandAimRotation(Vector3 targetPoint, Vector3 fallbackUp)
	{
		Transform muzzle = ActiveGun1 != null ? ActiveGun1.MuzzleTransform : null;
		if (muzzle == null)
		{
			Vector3 fallbackDirection = targetPoint - transform.position;
			if (fallbackDirection.sqrMagnitude <= Mathf.Epsilon)
			{
				return transform.rotation;
			}

			return Quaternion.LookRotation(fallbackDirection.normalized, fallbackUp);
		}

		Vector3 desiredDirection = targetPoint - muzzle.position;
		if (desiredDirection.sqrMagnitude <= Mathf.Epsilon)
		{
			return transform.rotation;
		}

		Vector3 desiredForward = desiredDirection.normalized;
		Quaternion parentRotation = transform.parent != null ? transform.parent.rotation : Quaternion.identity;
		Vector3 preferredUp = parentRotation * (initialLocalRotation * Vector3.up);
		Vector3 correctedUp = Vector3.ProjectOnPlane(preferredUp, desiredForward);

		// Keep a stable roll reference by projecting the authored local +Y onto the aim plane.
		if (correctedUp.sqrMagnitude <= 0.0001f)
		{
			correctedUp = fallbackUp;
			correctedUp = Vector3.ProjectOnPlane(correctedUp, desiredForward);
			if (correctedUp.sqrMagnitude <= 0.0001f)
			{
				correctedUp = Vector3.ProjectOnPlane(parentRotation * (initialLocalRotation * Vector3.right), desiredForward);
			}
		}
		correctedUp.Normalize();

		Quaternion desiredMuzzleRotation = Quaternion.LookRotation(desiredForward, correctedUp);
		Quaternion localMuzzleRotation = Quaternion.Inverse(transform.rotation) * muzzle.rotation;
		return desiredMuzzleRotation * Quaternion.Inverse(localMuzzleRotation);
	}

	private void DrawLaser(Vector3 startPosition, Vector3 endPosition)
	{
		float dist = Vector3.Distance(transform.position, endPosition);
		laser.startWidth = dist / 200;
		laser.endWidth = dist / 200;
		laser.SetPosition(0, startPosition);
		laser.SetPosition(1, endPosition);
	}

	private void UpdateAmmoIndicator()
	{
		if (ammoIndicator == null || ActiveGun1 == null) return;

		if (ActiveGun1.isReloading)
		{
			float full = ActiveGun1.gunData.shootConfig.reloadTime;
			float remaining = ActiveGun1.reloadTimeCache;

			// Safety
			if (full <= 0f) full = 0.0001f;

			// 0..1 where 0 = just started, 1 = finished
			float progress01 = 1f - Mathf.Clamp01(remaining / full);

			int filled = Mathf.RoundToInt(progress01 * ReloadBarWidth);
			filled = Mathf.Clamp(filled, 0, ReloadBarWidth);

			if (!hasAmmoIndicatorState || !lastAmmoIndicatorReloading || lastDisplayedReloadBarCount != filled)
			{
				ammoIndicator.text = ReloadIndicatorTexts[filled];
				hasAmmoIndicatorState = true;
				lastAmmoIndicatorReloading = true;
				lastDisplayedReloadBarCount = filled;
			}
		}
		else
		{
			int currentAmmo = ActiveGun1.currentShotsInMag;
			if (!hasAmmoIndicatorState || lastAmmoIndicatorReloading || lastDisplayedAmmoCount != currentAmmo)
			{
				ammoIndicator.text = currentAmmo.ToString();
				hasAmmoIndicatorState = true;
				lastAmmoIndicatorReloading = false;
				lastDisplayedAmmoCount = currentAmmo;
			}
		}
	}

	private static string[] BuildReloadIndicatorTexts()
	{
		string[] texts = new string[ReloadBarWidth + 1];
		for (int i = 0; i <= ReloadBarWidth; i++)
		{
			string bar = "[" + new string('█', i) + new string('░', ReloadBarWidth - i) + "]";
			texts[i] = $"RLD {bar}";
		}

		return texts;
	}

	private Transform ResolveAmmoIndicatorHeadTarget()
	{
		if (bodyController == null)
		{
			return null;
		}

		if (bodyController.headObjectTransformCache != null)
		{
			return bodyController.headObjectTransformCache;
		}

		if (bodyController.headObject != null)
		{
			return bodyController.headObject.transform;
		}

		if (bodyController.headObjectL != null)
		{
			return bodyController.headObjectL.transform;
		}

		return bodyController.transform;
	}

	private void RotateAmmoIndicatorTowardHead()
	{
		if (ammoIndicator == null)
		{
			return;
		}

		if (ammoIndicatorHeadTarget == null)
		{
			ammoIndicatorHeadTarget = ResolveAmmoIndicatorHeadTarget();
			if (ammoIndicatorHeadTarget == null)
			{
				return;
			}
		}

		Vector3 toHead = ammoIndicatorHeadTarget.position - ammoIndicator.transform.position;
		if (toHead.sqrMagnitude <= 0.0001f)
		{
			return;
		}

		Quaternion lookRotation = Quaternion.LookRotation(toHead.normalized, ammoIndicatorHeadTarget.up);
		ammoIndicator.transform.rotation = lookRotation * AmmoIndicatorFacingOffset;
	}

	private Gun CreateGun(GunType type, GameObject slot)
	{

		//Guns[index].Spawn(slot, this);
		//return Guns[index];
		GunDataScriptableObject gunData = Guns.Find(gun => gun.type == type);

		//if (gun == null)
		//{
		//    Debug.LogError($"No GunScriptableObject found for GunType: {gun}");
		//    return null;
		//}

		//gun.Spawn(slot, this);
		//return gun;

		GameObject gunObject = new GameObject(gunData.GunName);
		Gun gun = gunObject.AddComponent<Gun>();
		gun.gunData = gunData;
		gun.SetParent(slot, weapon);
		return gun;
	}

	void OnDrawGizmos()
	{
		Color color;
		color = Color.green;
		DrawHelperAtCenter((weapon.transform.up + -(weapon.transform.right * 0.5f)), color, 2f);

	}
	private void DrawHelperAtCenter(
								 Vector3 direction, Color color, float scale)
	{
		Gizmos.color = color;
		Vector3 destination = transform.position + direction * scale;
		Gizmos.DrawLine(transform.position, destination);
	}
}
