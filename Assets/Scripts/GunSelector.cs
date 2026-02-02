using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class GunSelector : MonoBehaviour
{
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
		ActiveGun1 = CreateGun(Gun1, Gun1Parent);
		ActiveGun2 = CreateGun(Gun2, Gun2Parent);
		ActiveGun3 = CreateGun(Gun3, Gun3Parent);
		Debug.Log("created guns");
		laser = GetComponent<LineRenderer>();
		isAI = GetComponentInParent<AIController>() != null ? true : false;
	}

	void Update()
	{
		if (Time.time - lastRaycastTime >= raycastInterval)
		{
			PerformRaycast();
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
			Vector3 targetDirection = targetPoint - gun.position;
			Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
			gun.rotation = targetRotation;
		}
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
		if (ammoIndicator == null) return;

		if (ActiveGun1.isReloading)
		{
			float full = ActiveGun1.gunData.shootConfig.reloadTime;
			float remaining = ActiveGun1.reloadTimeCache;

			// Safety
			if (full <= 0f) full = 0.0001f;

			// 0..1 where 0 = just started, 1 = finished
			float progress01 = 1f - Mathf.Clamp01(remaining / full);

			int barWidth = 6; // tweak to taste (8-14 usually reads well)
			int filled = Mathf.RoundToInt(progress01 * barWidth);
			filled = Mathf.Clamp(filled, 0, barWidth);

			string bar = "[" + new string('█', filled) + new string('░', barWidth - filled) + "]";

			// Optional: include percent (comment out if you want less cognitive load)
			int pct = Mathf.RoundToInt(progress01 * 100f);

			ammoIndicator.text = $"RLD {bar}";          // super minimal
																									// ammoIndicator.text = $"RLD {bar} {pct}%"; // optional
		}
		else
		{
			ammoIndicator.text = ActiveGun1.currentShotsInMag.ToString();
		}
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

