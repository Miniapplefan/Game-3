using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class GunSelector : MonoBehaviour
{
	private const int ReloadBarWidth = 6;
	private const string ActiveAimMarker = "●";
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
	private bool hasRaycastPoint;
	private Quaternion initialLocalRotation;
	private BodyController bodyController;
	private Transform ammoIndicatorHeadTarget;
	private bool hasAmmoIndicatorState;
	private bool lastAmmoIndicatorReloading;
	private int lastDisplayedAmmoCount;
	private int lastDisplayedReloadBarCount = -1;
	private string lastDisplayedAmmoIndicatorText;
	private bool lastSelectedArmIsLeft;
	private bool laserVisible = true;
	[SerializeField] private float minPrimaryAimDistance = 0.75f;
	[SerializeField] private Transform forearmForwardFallback;

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
		bool shouldDrawLaser = !isAI && IsOwningArmAimed();
		SetLaserVisible(shouldDrawLaser);
		if (shouldDrawLaser)
		{
			DrawPrimaryMuzzleLaser();
		}
		UpdateAmmoIndicator();
	}

	private void LateUpdate()
	{
	}

	void PerformRaycast()
	{
		Transform muzzle = GetPrimaryMuzzleTransform();
		if (muzzle == null)
		{
			return;
		}

		RaycastHit hit;
		float maxRange = GetPrimaryGunMaxRange();
		if (Physics.Raycast(muzzle.position, muzzle.forward, out hit, maxRange, raycastLayerMask))
		{
			raycastPoint = hit.point;
			hasRaycastPoint = true;
			// Rotate the guns to look at the hit point
			RotateGuns(hit.point);
		}
		else
		{
			raycastPoint = muzzle.position + muzzle.forward * maxRange;
			hasRaycastPoint = true;
			RotateGuns(raycastPoint);
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

		if (!TryGetPrimaryAimForward(muzzle, targetPoint, out Vector3 desiredForward))
		{
			return transform.rotation;
		}

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

	private bool TryGetPrimaryAimForward(Transform muzzle, Vector3 targetPoint, out Vector3 desiredForward)
	{
		Vector3 desiredDirection = targetPoint - muzzle.position;
		float minDistance = Mathf.Max(0f, minPrimaryAimDistance);
		if (desiredDirection.sqrMagnitude >= minDistance * minDistance)
		{
			desiredForward = desiredDirection.normalized;
			return true;
		}

		if (forearmForwardFallback == null || forearmForwardFallback.forward.sqrMagnitude <= 0.0001f)
		{
			desiredForward = Vector3.zero;
			return false;
		}

		desiredForward = forearmForwardFallback.forward.normalized;
		return true;
	}

	private void DrawPrimaryMuzzleLaser()
	{
		Transform muzzle = GetPrimaryMuzzleTransform();
		if (muzzle == null || laser == null)
		{
			CollapseLaser();
			return;
		}

		if (!hasRaycastPoint)
		{
			raycastPoint = muzzle.position + muzzle.forward * GetPrimaryGunMaxRange();
			hasRaycastPoint = true;
		}

		float dist = Vector3.Distance(muzzle.position, raycastPoint);
		Vector3 startPosition = muzzle.position;
		Vector3 endPosition = muzzle.position + muzzle.forward * dist;
		DrawLaser(startPosition, endPosition, dist);
	}

	private bool IsOwningArmAimed()
	{
		if (bodyController == null)
		{
			bodyController = GetComponentInParent<BodyController>();
			if (bodyController == null)
			{
				return true;
			}
		}

		if (bodyController.guns == this)
		{
			return bodyController.IsRightArmAimed;
		}

		if (bodyController.gunsL == this)
		{
			return bodyController.IsLeftArmAimed;
		}

		return true;
	}

	private void SetLaserVisible(bool visible)
	{
		if (laser == null)
		{
			return;
		}

		if (laserVisible == visible && laser.enabled == visible)
		{
			return;
		}

		laserVisible = visible;
		laser.enabled = visible;
	}

	private Transform GetPrimaryMuzzleTransform()
	{
		return ActiveGun1 != null ? ActiveGun1.MuzzleTransform : null;
	}

	private float GetPrimaryGunMaxRange()
	{
		return ActiveGun1 != null && ActiveGun1.gunData != null
			? ActiveGun1.gunData.shootConfig.maxRange
			: 0f;
	}

	private void CollapseLaser()
	{
		if (laser == null)
		{
			return;
		}

		laser.SetPosition(0, transform.position);
		laser.SetPosition(1, transform.position);
	}

	private void DrawLaser(Vector3 startPosition, Vector3 endPosition, float dist)
	{
		// laser.startWidth = dist / 200;
		// laser.endWidth = dist / 200;
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

			bool selectedArmIsLeft = IsSelectedArmLeft();
			string displayText = FormatAmmoIndicatorText(ReloadIndicatorTexts[filled]);
			if (!hasAmmoIndicatorState
				|| !lastAmmoIndicatorReloading
				|| lastDisplayedReloadBarCount != filled
				|| lastDisplayedAmmoIndicatorText != displayText
				|| lastSelectedArmIsLeft != selectedArmIsLeft)
			{
				ammoIndicator.text = displayText;
				hasAmmoIndicatorState = true;
				lastAmmoIndicatorReloading = true;
				lastDisplayedReloadBarCount = filled;
				lastDisplayedAmmoIndicatorText = displayText;
				lastSelectedArmIsLeft = selectedArmIsLeft;
			}
		}
		else
		{
			int currentAmmo = ActiveGun1.currentShotsInMag;
			bool selectedArmIsLeft = IsSelectedArmLeft();
			string displayText = FormatAmmoIndicatorText(currentAmmo.ToString());
			if (!hasAmmoIndicatorState
				|| lastAmmoIndicatorReloading
				|| lastDisplayedAmmoCount != currentAmmo
				|| lastDisplayedAmmoIndicatorText != displayText
				|| lastSelectedArmIsLeft != selectedArmIsLeft)
			{
				ammoIndicator.text = displayText;
				hasAmmoIndicatorState = true;
				lastAmmoIndicatorReloading = false;
				lastDisplayedAmmoCount = currentAmmo;
				lastDisplayedAmmoIndicatorText = displayText;
				lastSelectedArmIsLeft = selectedArmIsLeft;
			}
		}
	}

	private string FormatAmmoIndicatorText(string text)
	{
		if (bodyController == null)
		{
			bodyController = GetComponentInParent<BodyController>();
			if (bodyController == null)
			{
				return text;
			}
		}

		bool selectedArmIsLeft = IsSelectedArmLeft();
		if (bodyController.guns == this && !selectedArmIsLeft)
		{
			return ActiveAimMarker + text;
		}

		if (bodyController.gunsL == this && selectedArmIsLeft)
		{
			return text + ActiveAimMarker;
		}

		return text;
	}

	private bool IsSelectedArmLeft()
	{
		return bodyController.PrimaryAimUsesLeft;
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
