using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class GunAudioEmitter : MonoBehaviour
{
	private Gun gun;
	private GameAudioCueId gunshotCueId;
	private bool warnedMissingMuzzle;

	public void Initialize(Gun owningGun, bool isAIControlled)
	{
		if (gun != null)
		{
			gun.ActualShotFired -= OnActualShotFired;
			gun.EmptyTriggerPulled -= OnEmptyTriggerPulled;
			gun.EnemyHitByPlayer -= OnEnemyHitByPlayer;
			gun.ReloadStarted -= OnReloadStarted;
		}

		gun = owningGun;
		gunshotCueId = isAIControlled ? GameAudioCueId.EnemyGunshot : GameAudioCueId.PlayerGunshot;
		warnedMissingMuzzle = false;

		if (gun != null)
		{
			gun.ActualShotFired += OnActualShotFired;
			if (!isAIControlled)
			{
				gun.EmptyTriggerPulled += OnEmptyTriggerPulled;
				gun.EnemyHitByPlayer += OnEnemyHitByPlayer;
				gun.ReloadStarted += OnReloadStarted;
			}
		}
	}

	private void OnDestroy()
	{
		if (gun != null)
		{
			gun.ActualShotFired -= OnActualShotFired;
			gun.EmptyTriggerPulled -= OnEmptyTriggerPulled;
			gun.EnemyHitByPlayer -= OnEnemyHitByPlayer;
			gun.ReloadStarted -= OnReloadStarted;
		}
	}

	private void OnActualShotFired(Gun firedGun)
	{
		if (firedGun == null || firedGun != gun)
		{
			return;
		}

		Transform muzzle = firedGun.MuzzleTransform;
		if (muzzle == null)
		{
			if (!warnedMissingMuzzle)
			{
				warnedMissingMuzzle = true;
				Debug.LogWarning($"Cannot play {gunshotCueId} because {firedGun.name} has no muzzle transform.", firedGun);
			}
			return;
		}

		AudioService.PlayAt(gunshotCueId, muzzle.position);
	}

	private void OnEnemyHitByPlayer(Gun firedGun, EnemyHitByPlayerInfo hitInfo)
	{
		if (firedGun == null || firedGun != gun || hitInfo.Target == null)
		{
			return;
		}

		AudioService.PlayAt(GameAudioCueId.EnemyHitByPlayer, hitInfo.ImpactPosition);
	}

	private void OnEmptyTriggerPulled(Gun emptyGun)
	{
		if (emptyGun == null || emptyGun != gun)
		{
			return;
		}

		Transform followTarget = emptyGun.ModelRoot != null
			? emptyGun.ModelRoot
			: emptyGun.transform;
		AudioService.PlayFollowing(GameAudioCueId.PlayerEmptyGunClick, followTarget);
	}

	private void OnReloadStarted(Gun reloadingGun, GunReloadAudioInfo reloadInfo)
	{
		ScheduleReloadCue(reloadingGun, GameAudioCueId.PlayerReloadStarted, reloadInfo.DelaySeconds);

		PreparedAudioCue finishCue = AudioService.PrepareOneShot(GameAudioCueId.PlayerReloadFinished);
		if (finishCue != null)
		{
			StartCoroutine(PlayReloadFinishCue(reloadingGun, finishCue));
		}
	}

	private void ScheduleReloadCue(Gun reloadingGun, GameAudioCueId cueId, float delaySeconds)
	{
		if (reloadingGun == null || reloadingGun != gun)
		{
			return;
		}

		StartCoroutine(PlayReloadCueAfterDelay(reloadingGun, cueId, delaySeconds));
	}

	private IEnumerator PlayReloadCueAfterDelay(Gun reloadingGun, GameAudioCueId cueId, float delaySeconds)
	{
		float elapsed = 0f;
		while (elapsed < delaySeconds)
		{
			elapsed += BulletTimeManager.GetDeltaTime(BulletTimeChannel.PlayerFireRate);
			yield return null;
		}

		if (reloadingGun == null || reloadingGun != gun)
		{
			yield break;
		}

		Transform followTarget = reloadingGun.ModelRoot != null
			? reloadingGun.ModelRoot
			: reloadingGun.transform;
		AudioService.PlayFollowing(cueId, followTarget);
	}

	private IEnumerator PlayReloadFinishCue(Gun reloadingGun, PreparedAudioCue finishCue)
	{
		float playbackDuration = finishCue.DurationSeconds;
		while (reloadingGun != null
			&& reloadingGun == gun
			&& reloadingGun.isReloading
			&& reloadingGun.reloadTimeCache > playbackDuration)
		{
			yield return null;
		}

		if (reloadingGun == null || reloadingGun != gun || !reloadingGun.isReloading)
		{
			yield break;
		}

		Transform followTarget = reloadingGun.ModelRoot != null
			? reloadingGun.ModelRoot
			: reloadingGun.transform;
		if (AudioService.PlayFollowing(finishCue, followTarget))
		{
			reloadingGun.AlignReloadCompletionToAudio(playbackDuration);
		}
	}
}
