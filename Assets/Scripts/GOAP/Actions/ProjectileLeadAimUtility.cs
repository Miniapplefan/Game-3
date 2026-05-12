using UnityEngine;

public static class ProjectileLeadAimUtility
{
	public readonly struct NpcLeadAimPoints
	{
		public NpcLeadAimPoints(Vector3 visualAimPoint, Vector3 ballisticAimPoint, bool hasBallisticAimPoint)
		{
			VisualAimPoint = visualAimPoint;
			BallisticAimPoint = ballisticAimPoint;
			HasBallisticAimPoint = hasBallisticAimPoint;
		}

		public Vector3 VisualAimPoint { get; }
		public Vector3 BallisticAimPoint { get; }
		public bool HasBallisticAimPoint { get; }
	}

	public static NpcLeadAimPoints GetNpcAimPoints(Gun gun, BodyState targetState, Vector3 shooterPosition, Vector3 rawAimPoint)
	{
		if (gun == null || gun.gunData == null || gun.gunData.shootConfig == null || targetState == null)
		{
			return new NpcLeadAimPoints(rawAimPoint, rawAimPoint, false);
		}

		ShootConfigScriptableObject shootConfig = gun.gunData.shootConfig;
		if (!shootConfig.enableNpcLeadAim || (shootConfig.npcVisualLeadStrength <= 0f && shootConfig.npcBallisticLeadStrength <= 0f))
		{
			return new NpcLeadAimPoints(rawAimPoint, rawAimPoint, false);
		}

		Vector3 targetVelocity = targetState.rb != null ? targetState.rb.velocity : Vector3.zero;
		if (targetVelocity.sqrMagnitude < shootConfig.npcMinLeadTargetSpeed * shootConfig.npcMinLeadTargetSpeed)
		{
			return new NpcLeadAimPoints(rawAimPoint, rawAimPoint, false);
		}

		float projectileSpeed = GetNpcProjectileSpeed(gun);
		if (projectileSpeed <= 0.001f)
		{
			return new NpcLeadAimPoints(rawAimPoint, rawAimPoint, false);
		}

		if (!TryGetInterceptTime(rawAimPoint - shooterPosition, targetVelocity, projectileSpeed, out float interceptTime))
		{
			return new NpcLeadAimPoints(rawAimPoint, rawAimPoint, false);
		}

		interceptTime = Mathf.Min(interceptTime, Mathf.Max(0f, shootConfig.npcMaxLeadTime));
		Vector3 predictedAimPoint = rawAimPoint + targetVelocity * interceptTime;
		Vector3 visualAimPoint = Vector3.Lerp(rawAimPoint, predictedAimPoint, Mathf.Clamp01(shootConfig.npcVisualLeadStrength));
		Vector3 ballisticAimPoint = Vector3.Lerp(rawAimPoint, predictedAimPoint, Mathf.Clamp01(shootConfig.npcBallisticLeadStrength));
		return new NpcLeadAimPoints(visualAimPoint, ballisticAimPoint, shootConfig.npcBallisticLeadStrength > 0f);
	}

	public static Vector3 GetShooterPosition(Gun gun, Vector3 fallbackPosition)
	{
		if (gun != null && gun.MuzzleTransform != null)
		{
			return gun.MuzzleTransform.position;
		}

		return fallbackPosition;
	}

	private static float GetNpcProjectileSpeed(Gun gun)
	{
		if (gun == null || gun.gunData == null || gun.gunData.npcBulletPrefab == null)
		{
			return 0f;
		}

		Bullet bullet = gun.gunData.npcBulletPrefab.GetComponent<Bullet>();
		if (bullet == null)
		{
			bullet = gun.gunData.npcBulletPrefab.GetComponentInChildren<Bullet>();
		}

		return bullet != null ? bullet.speed : 0f;
	}

	private static bool TryGetInterceptTime(Vector3 relativePosition, Vector3 targetVelocity, float projectileSpeed, out float interceptTime)
	{
		float a = Vector3.Dot(targetVelocity, targetVelocity) - projectileSpeed * projectileSpeed;
		float b = 2f * Vector3.Dot(relativePosition, targetVelocity);
		float c = Vector3.Dot(relativePosition, relativePosition);

		if (Mathf.Abs(a) < 0.0001f)
		{
			if (Mathf.Abs(b) < 0.0001f)
			{
				interceptTime = 0f;
				return false;
			}

			interceptTime = -c / b;
			return interceptTime > 0f;
		}

		float discriminant = b * b - 4f * a * c;
		if (discriminant < 0f)
		{
			interceptTime = 0f;
			return false;
		}

		float sqrtDiscriminant = Mathf.Sqrt(discriminant);
		float t1 = (-b - sqrtDiscriminant) / (2f * a);
		float t2 = (-b + sqrtDiscriminant) / (2f * a);
		interceptTime = GetSmallestPositiveTime(t1, t2);
		return interceptTime > 0f;
	}

	private static float GetSmallestPositiveTime(float t1, float t2)
	{
		bool t1Positive = t1 > 0f;
		bool t2Positive = t2 > 0f;
		if (t1Positive && t2Positive)
		{
			return Mathf.Min(t1, t2);
		}

		if (t1Positive)
		{
			return t1;
		}

		return t2Positive ? t2 : 0f;
	}

}
