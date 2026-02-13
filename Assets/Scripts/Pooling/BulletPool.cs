using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public static class BulletPool
{
	private static readonly Dictionary<int, ObjectPool<Bullet>> Pools = new Dictionary<int, ObjectPool<Bullet>>();

	public static Bullet Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool enableTrail = true)
	{
		if (prefab == null)
		{
			return null;
		}

		int key = prefab.GetInstanceID();
		if (!Pools.TryGetValue(key, out ObjectPool<Bullet> pool))
		{
			if (prefab.GetComponent<Bullet>() == null && prefab.GetComponentInChildren<Bullet>() == null)
			{
				Debug.LogError($"BulletPool: Prefab {prefab.name} has no Bullet component.");
				return null;
			}

			ObjectPool<Bullet> createdPool = null;
			createdPool = new ObjectPool<Bullet>(
				() => CreateBullet(prefab, createdPool),
				OnGetBullet,
				OnReleaseBullet,
				OnDestroyBullet,
				collectionCheck: false,
				defaultCapacity: 16,
				maxSize: 256
			);
			pool = createdPool;
			Pools.Add(key, pool);
		}

		Bullet bullet = pool.Get();
		if (bullet == null)
		{
			return null;
		}

		bullet.transform.SetPositionAndRotation(position, rotation);
		bullet.SetTrailEnabled(enableTrail);
		return bullet;
	}

	private static Bullet CreateBullet(GameObject prefab, ObjectPool<Bullet> pool)
	{
		GameObject instance = Object.Instantiate(prefab);
		Bullet bullet = instance.GetComponent<Bullet>();
		if (bullet == null)
		{
			bullet = instance.GetComponentInChildren<Bullet>();
		}
		if (bullet == null)
		{
			Debug.LogError($"BulletPool: Prefab {prefab.name} has no Bullet component on instance.");
			Object.Destroy(instance);
			return null;
		}

		bullet.SetPoolRelease(pool.Release);
		instance.SetActive(false);
		return bullet;
	}

	private static void OnGetBullet(Bullet bullet)
	{
		if (bullet != null)
		{
			bullet.gameObject.SetActive(true);
		}
	}

	private static void OnReleaseBullet(Bullet bullet)
	{
		if (bullet != null)
		{
			bullet.gameObject.SetActive(false);
		}
	}

	private static void OnDestroyBullet(Bullet bullet)
	{
		if (bullet != null)
		{
			Object.Destroy(bullet.gameObject);
		}
	}
}
