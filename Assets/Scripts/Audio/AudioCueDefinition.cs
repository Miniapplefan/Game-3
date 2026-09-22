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
	PlayerEmptyGunClick,
	BulletTimeStarted,
	BulletTimeEnding,
	PlayerGraze
}

[Serializable]
public sealed class AudioClipVariant
{
	[SerializeField] private AudioClip clip;
	[SerializeField] private AudioMixerGroup outputMixerGroup;
	[SerializeField, Min(0f)] private float selectionWeight = 1f;

	public AudioClip Clip => clip;
	public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
	public float SelectionWeight => Mathf.Max(0f, selectionWeight);

	public void Validate()
	{
		selectionWeight = Mathf.Max(0f, selectionWeight);
	}
}

[Serializable]
public sealed class AudioCueDefinition
{
	[SerializeField] private List<AudioClipVariant> variants = new List<AudioClipVariant>();
	[SerializeField] private AudioMixerGroup outputMixerGroup;
	[SerializeField, Range(0f, 1f)] private float volume = 1f;
	[SerializeField, Min(0)] private int maxSimultaneousOneShots;
	[SerializeField, Min(0f)] private float minRetriggerSeconds;
	[SerializeField, Min(0f)] private float successivePitchWindowSeconds;
	[SerializeField, Min(0f)] private float successivePitchStepSemitones;
	[SerializeField, Min(0)] private int successivePitchMaxSteps;
	[SerializeField] private Vector2 pitchRange = new Vector2(0.97f, 1.03f);
	[SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
	[SerializeField, Min(0.01f)] private float minDistance = 2f;
	[SerializeField, Min(0.01f)] private float maxDistance = 30f;
	[SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
	[SerializeField, Range(0, 256)] private int priority = 128;
	[SerializeField] private bool loop;
	[SerializeField, Min(0f)] private float fadeInSeconds;
	[SerializeField, Min(0f)] private float fadeOutSeconds;

	public IReadOnlyList<AudioClipVariant> Variants => variants;
	public AudioMixerGroup OutputMixerGroup => outputMixerGroup;
	public float Volume => Mathf.Clamp01(volume);
	public int MaxSimultaneousOneShots => Mathf.Max(0, maxSimultaneousOneShots);
	public float MinRetriggerSeconds => Mathf.Max(0f, minRetriggerSeconds);
	public float SuccessivePitchWindowSeconds => Mathf.Max(0f, successivePitchWindowSeconds);
	public float SuccessivePitchStepSemitones => Mathf.Max(0f, successivePitchStepSemitones);
	public int SuccessivePitchMaxSteps => Mathf.Max(0, successivePitchMaxSteps);
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
		if (variants == null)
		{
			return false;
		}

		for (int i = 0; i < variants.Count; i++)
		{
			AudioClipVariant variant = variants[i];
			if (variant != null && variant.Clip != null && variant.SelectionWeight > 0f)
			{
				return true;
			}
		}

		return false;
	}

	public void Validate()
	{
		if (variants == null)
		{
			variants = new List<AudioClipVariant>();
		}

		for (int i = 0; i < variants.Count; i++)
		{
			variants[i]?.Validate();
		}

		volume = Mathf.Clamp01(volume);
		maxSimultaneousOneShots = Mathf.Max(0, maxSimultaneousOneShots);
		minRetriggerSeconds = Mathf.Max(0f, minRetriggerSeconds);
		successivePitchWindowSeconds = Mathf.Max(0f, successivePitchWindowSeconds);
		successivePitchStepSemitones = Mathf.Max(0f, successivePitchStepSemitones);
		successivePitchMaxSteps = Mathf.Max(0, successivePitchMaxSteps);
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
