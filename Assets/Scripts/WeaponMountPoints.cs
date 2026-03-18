using UnityEngine;

[DisallowMultipleComponent]
public class WeaponMountPoints : MonoBehaviour
{
	[SerializeField]
	private Transform grip;

	[SerializeField]
	private Transform muzzle;

	public Transform Grip => grip;
	public Transform Muzzle => muzzle;

	private void OnValidate()
	{
		if (grip == null)
		{
			Transform candidate = transform.Find("Grip");
			if (candidate != null)
			{
				grip = candidate;
			}
		}

		if (muzzle == null)
		{
			Transform candidate = transform.Find("Muzzle");
			if (candidate != null)
			{
				muzzle = candidate;
			}
		}
	}
}
