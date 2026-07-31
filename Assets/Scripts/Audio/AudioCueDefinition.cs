using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public enum GameAudioCueId
{
	PlayerGunshot,
	EnemyGunshot,
	EnemyHitByPlayer,
	EnemyLethalBulletWarning,
	PlayerReloadStarted,
	PlayerReloadFinished,
	PlayerEmptyGunClick
}

[Serializable]
public sealed class AudioCueDefinition
{
	[SerializeField] private List<AudioClip> clips = new List<AudioClip>();
	[SerializeField] private AudioMixerGroup outputMixerGroup;
	[SerializeField, Range(0f, 1f)] private float volume = 1f;
	[SerializeField] private Vector2 pitchRange = new Vector2(0.97f, 1.03f);
	[SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
	[SerializeField, Min(0.01f)] private float minDistance = 2f;
	[SerializeField, Min(0.01f)] private float maxDistance = 30f;
	[SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
	[SerializeField, Range(0, 256)] private int priority = 128;
	[SerializeField] private bool loop;
	[SerializeField, Min(0f)] private float fadeInSeconds;
	[SerializeField, Min(0f)] private float fadeOutSeconds;

	public IReadOnlyList<AudioClip> Clips => clips;
	public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
	public float Volume => Mathf.Clamp01(volume);
	public float MinPitch => Mathf.Min(pitchRange.x, pitchRange.y);
	public float MaxPitch => Mathf.Max(pitchRange.x, pitchRange.y);
	public float SpatialBlend => Mathf.Clamp01(spatialBlend);
	public float MinDistance => Mathf.Max(0.01f, minDistance);
	public float MaxDistance => Mathf.Max(MinDistance, maxDistance);
	public AudioRolloffMode RolloffMode => rolloffMode;
	public int Priority => Mathf.Clamp(priority, 0, 256);
	public bool Loop => loop;
	public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
	public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);

	public bool HasAnyClip()
	{
		if (clips == null)
		{
			return false;
		}

		for (int i = 0; i < clips.Count; i++)
		{
			if (clips[i] != null)
			{
				return true;
			}
		}

		return false;
	}

	public void Validate()
	{
		if (clips == null)
		{
			clips = new List<AudioClip>();
		}

		volume = Mathf.Clamp01(volume);
		pitchRange.x = Mathf.Clamp(pitchRange.x, -3f, 3f);
		pitchRange.y = Mathf.Clamp(pitchRange.y, -3f, 3f);
		spatialBlend = Mathf.Clamp01(spatialBlend);
		minDistance = Mathf.Max(0.01f, minDistance);
		maxDistance = Mathf.Max(minDistance, maxDistance);
		priority = Mathf.Clamp(priority, 0, 256);
		fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
		fadeOutSeconds = Mathf.Max(0f, fadeOutSeconds);
	}
}
