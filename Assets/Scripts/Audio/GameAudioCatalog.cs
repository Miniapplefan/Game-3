using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "GameAudioCatalog", menuName = "Audio/Game Audio Catalog", order = 0)]
public sealed class GameAudioCatalog : ScriptableObject
{
	[SerializeField] private AudioMixerSnapshot defaultSnapshot;
	[SerializeField] private AudioMixerSnapshot bulletTimeSnapshot;
	[SerializeField] private AudioCueDefinition playerGunshot = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition playerEmptyGunClick = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition playerReloadStarted = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition playerReloadFinished = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition bulletTimeStarted = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition bulletTimeEnding = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition enemyGunshot = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition enemyHitByPlayer = new AudioCueDefinition();
	[SerializeField] private AudioCueDefinition enemyLethalBulletWarning = new AudioCueDefinition();

	public AudioMixerSnapshot DefaultSnapshot => defaultSnapshot;
	public AudioMixerSnapshot BulletTimeSnapshot => bulletTimeSnapshot;

	public bool TryGetCue(GameAudioCueId cueId, out AudioCueDefinition cue)
	{
		switch (cueId)
		{
			case GameAudioCueId.PlayerGunshot:
				cue = playerGunshot;
				return cue != null;
			case GameAudioCueId.PlayerEmptyGunClick:
				cue = playerEmptyGunClick;
				return cue != null;
			case GameAudioCueId.PlayerReloadStarted:
				cue = playerReloadStarted;
				return cue != null;
			case GameAudioCueId.PlayerReloadFinished:
				cue = playerReloadFinished;
				return cue != null;
			case GameAudioCueId.BulletTimeStarted:
				cue = bulletTimeStarted;
				return cue != null;
			case GameAudioCueId.BulletTimeEnding:
				cue = bulletTimeEnding;
				return cue != null;
			case GameAudioCueId.EnemyGunshot:
				cue = enemyGunshot;
				return cue != null;
			case GameAudioCueId.EnemyHitByPlayer:
				cue = enemyHitByPlayer;
				return cue != null;
			case GameAudioCueId.EnemyLethalBulletWarning:
				cue = enemyLethalBulletWarning;
				return cue != null;
			default:
				cue = null;
				return false;
		}
	}

	private void OnValidate()
	{
		playerGunshot?.Validate();
		playerEmptyGunClick?.Validate();
		playerReloadStarted?.Validate();
		playerReloadFinished?.Validate();
		bulletTimeStarted?.Validate();
		bulletTimeEnding?.Validate();
		enemyGunshot?.Validate();
		enemyHitByPlayer?.Validate();
		enemyLethalBulletWarning?.Validate();
	}
}
