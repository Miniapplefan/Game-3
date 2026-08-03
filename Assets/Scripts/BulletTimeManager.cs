using UnityEngine;
using UnityEngine.Serialization;

public enum BulletTimeChannel
{
	EnemyMovement,
	EnemyBullet,
	EnemyHitReaction,
	EnemyFireRate,
	PlayerMovement,
	PlayerFireRate,
	PlayerActiveRagdoll,
	PlayerAura,
	PlayerAuraGrip,
	PlayerPulseRecharge
}

public class BulletTimeManager : MonoBehaviour
{
	private static BulletTimeManager instance;

	[Header("Timing")]
	[SerializeField] private float duration = 0.35f;
	[FormerlySerializedAs("scale")]
	[SerializeField, Range(0f, 1f)] private float defaultScale = 0.25f;
	[SerializeField] private AnimationCurve intensityCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

	[Header("Enemy Channels")]
	[SerializeField] private bool affectEnemyMovement = true;
	[SerializeField, Range(0f, 1f)] private float enemyMovementScale = 0.25f;
	[SerializeField] private bool affectEnemyBullets = true;
	[SerializeField, Range(0f, 1f)] private float enemyBulletScale = 0.25f;
	[SerializeField] private bool affectEnemyHitReactions = true;
	[SerializeField, Range(0f, 1f)] private float enemyHitReactionScale = 0.25f;
	[SerializeField] private bool affectEnemyFireRate = true;
	[SerializeField, Range(0f, 1f)] private float enemyFireRateScale = 0.25f;

	[Header("Player Channels")]
	[SerializeField] private bool affectPlayerMovement = false;
	[SerializeField, Range(0f, 1f)] private float playerMovementScale = 0.25f;
	[SerializeField] private bool affectPlayerFireRate = false;
	[SerializeField, Range(0f, 1f)] private float playerFireRateScale = 0.25f;
	[SerializeField] private bool affectPlayerActiveRagdoll = false;
	[SerializeField, Range(0f, 1f)] private float playerActiveRagdollScale = 0.25f;
	[SerializeField] private bool affectPlayerAura = false;
	[SerializeField, Range(0f, 1f)] private float playerAuraScale = 0.25f;
	[SerializeField] private bool affectPlayerAuraGrip = false;
	[SerializeField, Range(0f, 1f)] private float playerAuraGripScale = 0.25f;
	[SerializeField] private bool affectPlayerPulseRecharge = true;
	[SerializeField, Range(0f, 1f)] private float playerPulseRechargeScale = 0.25f;

	private float elapsed;
	private bool active;
	private int triggerVersion;

	public static bool IsActive => EnsureInstance().active;
	public static float Duration => Mathf.Max(0f, EnsureInstance().duration);
	public static float RemainingTime
	{
		get
		{
			BulletTimeManager manager = EnsureInstance();
			return manager.active ? Mathf.Max(0f, manager.duration - manager.elapsed) : 0f;
		}
	}
	public static int TriggerVersion => EnsureInstance().triggerVersion;

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Destroy(gameObject);
			return;
		}

		instance = this;
	}

	private void OnDestroy()
	{
		if (instance == this)
		{
			instance = null;
		}
	}

	private void Update()
	{
		if (!active)
		{
			return;
		}

		elapsed += Time.unscaledDeltaTime;
		if (elapsed >= Mathf.Max(0f, duration))
		{
			active = false;
			elapsed = 0f;
		}
	}

	public static void Trigger()
	{
		EnsureInstance().StartBulletTime();
	}

	public static float GetScale(BulletTimeChannel channel)
	{
		BulletTimeManager manager = EnsureInstance();
		if (!manager.active || !manager.IsChannelEnabled(channel))
		{
			return 1f;
		}

		float durationSafe = Mathf.Max(0.0001f, manager.duration);
		float normalizedTime = Mathf.Clamp01(manager.elapsed / durationSafe);
		float intensity = manager.intensityCurve != null
			? Mathf.Clamp01(manager.intensityCurve.Evaluate(normalizedTime))
			: 1f;
		return Mathf.Lerp(1f, manager.GetChannelScale(channel), intensity);
	}

	public static float GetDeltaTime(BulletTimeChannel channel)
	{
		return Time.deltaTime * GetScale(channel);
	}

	private static BulletTimeManager EnsureInstance()
	{
		if (instance != null)
		{
			return instance;
		}

		instance = FindObjectOfType<BulletTimeManager>();
		if (instance != null)
		{
			return instance;
		}

		GameObject managerObject = new GameObject("BulletTimeManager");
		instance = managerObject.AddComponent<BulletTimeManager>();
		return instance;
	}

	private void StartBulletTime()
	{
		elapsed = 0f;
		active = duration > 0f;
		if (active)
		{
			triggerVersion++;
		}
	}

	private bool IsChannelEnabled(BulletTimeChannel channel)
	{
		switch (channel)
		{
			case BulletTimeChannel.EnemyMovement:
				return affectEnemyMovement;
			case BulletTimeChannel.EnemyBullet:
				return affectEnemyBullets;
			case BulletTimeChannel.EnemyHitReaction:
				return affectEnemyHitReactions;
			case BulletTimeChannel.EnemyFireRate:
				return affectEnemyFireRate;
			case BulletTimeChannel.PlayerMovement:
				return affectPlayerMovement;
			case BulletTimeChannel.PlayerFireRate:
				return affectPlayerFireRate;
			case BulletTimeChannel.PlayerActiveRagdoll:
				return affectPlayerActiveRagdoll;
			case BulletTimeChannel.PlayerAura:
				return affectPlayerAura;
			case BulletTimeChannel.PlayerAuraGrip:
				return affectPlayerAuraGrip;
			case BulletTimeChannel.PlayerPulseRecharge:
				return affectPlayerPulseRecharge;
			default:
				return false;
		}
	}

	private float GetChannelScale(BulletTimeChannel channel)
	{
		switch (channel)
		{
			case BulletTimeChannel.EnemyMovement:
				return Mathf.Clamp01(enemyMovementScale);
			case BulletTimeChannel.EnemyBullet:
				return Mathf.Clamp01(enemyBulletScale);
			case BulletTimeChannel.EnemyHitReaction:
				return Mathf.Clamp01(enemyHitReactionScale);
			case BulletTimeChannel.EnemyFireRate:
				return Mathf.Clamp01(enemyFireRateScale);
			case BulletTimeChannel.PlayerMovement:
				return Mathf.Clamp01(playerMovementScale);
			case BulletTimeChannel.PlayerFireRate:
				return Mathf.Clamp01(playerFireRateScale);
			case BulletTimeChannel.PlayerActiveRagdoll:
				return Mathf.Clamp01(playerActiveRagdollScale);
			case BulletTimeChannel.PlayerAura:
				return Mathf.Clamp01(playerAuraScale);
			case BulletTimeChannel.PlayerAuraGrip:
				return Mathf.Clamp01(playerAuraGripScale);
			case BulletTimeChannel.PlayerPulseRecharge:
				return Mathf.Clamp01(playerPulseRechargeScale);
			default:
				return Mathf.Clamp01(defaultScale);
		}
	}
}
