using System.Collections.Generic;
using UnityEngine;

public sealed class AudioOneShotHandle
{
	private AudioService owner;
	private readonly int voiceIndex;
	private readonly int generation;

	internal AudioOneShotHandle(AudioService owner, int voiceIndex, int generation)
	{
		this.owner = owner;
		this.voiceIndex = voiceIndex;
		this.generation = generation;
	}

	public bool IsValid => owner != null && owner.IsOneShotHandleValid(voiceIndex, generation);

	public void FadeOut(float seconds)
	{
		if (owner != null)
		{
			owner.FadeOutOneShot(voiceIndex, generation, seconds);
		}
	}

	internal void Invalidate(AudioService expectedOwner)
	{
		if (owner == expectedOwner)
		{
			owner = null;
		}
	}
}

public sealed class AudioLoopHandle
{
	private AudioService owner;
	private readonly int voiceIndex;
	private readonly int generation;

	internal AudioLoopHandle(AudioService owner, int voiceIndex, int generation)
	{
		this.owner = owner;
		this.voiceIndex = voiceIndex;
		this.generation = generation;
	}

	public bool IsValid => owner != null && owner.IsLoopHandleValid(voiceIndex, generation);

	public void SetActive(bool active)
	{
		if (owner != null)
		{
			owner.SetLoopHandleActive(voiceIndex, generation, active);
		}
	}

	public void Release()
	{
		AudioService currentOwner = owner;
		owner = null;
		if (currentOwner != null)
		{
			currentOwner.ReleaseLoopHandle(voiceIndex, generation);
		}
	}

	internal void Invalidate(AudioService expectedOwner)
	{
		if (owner == expectedOwner)
		{
			owner = null;
		}
	}
}

public sealed class PreparedAudioCue
{
	internal AudioService Owner { get; }
	internal AudioCueDefinition Definition { get; }
	internal AudioClip Clip { get; }
	internal float Pitch { get; }
	internal bool Consumed { get; set; }

	public float DurationSeconds => Clip != null
		? Clip.length / Mathf.Max(0.01f, Mathf.Abs(Pitch))
		: 0f;

	internal PreparedAudioCue(AudioService owner, AudioCueDefinition definition, AudioClip clip, float pitch)
	{
		Owner = owner;
		Definition = definition;
		Clip = clip;
		Pitch = pitch;
	}
}

[DisallowMultipleComponent]
public sealed class AudioService : MonoBehaviour
{
	private const string CatalogResourcePath = "Audio/GameAudioCatalog";
	private const int VoiceCount = 16;
	private const int LoopVoiceCount = 16;

	private sealed class Voice
	{
		public AudioSource Source;
		public Transform Target;
		public AudioOneShotHandle Handle;
		public int Generation;
		public float StartedAt;
		public int Priority;
		public float FadeStartVolume;
		public float FadeDuration;
		public float FadeElapsed;
	}

	private sealed class LoopVoice
	{
		public AudioSource Source;
		public Transform Target;
		public AudioLoopHandle Handle;
		public int Generation;
		public float BaseVolume;
		public float FadeInSeconds;
		public float FadeOutSeconds;
		public float FadeStartVolume;
		public float FadeTargetVolume;
		public float FadeDuration;
		public float FadeElapsed;
		public bool ReleaseWhenFadeCompletes;
		public bool Active;
	}

	private static AudioService instance;
	private readonly List<Voice> voices = new List<Voice>(VoiceCount);
	private readonly List<LoopVoice> loopVoices = new List<LoopVoice>(LoopVoiceCount);
	private readonly Dictionary<GameAudioCueId, int> lastClipIndices = new Dictionary<GameAudioCueId, int>();
	private readonly HashSet<string> warnings = new HashSet<string>();
	private GameAudioCatalog catalog;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStatics()
	{
		instance = null;
	}

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	private static void Bootstrap()
	{
		EnsureInstance();
	}

	private void Awake()
	{
		if (instance != null && instance != this)
		{
			Destroy(gameObject);
			return;
		}

		instance = this;
		gameObject.name = nameof(AudioService);
		transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
		DontDestroyOnLoad(gameObject);

		catalog = Resources.Load<GameAudioCatalog>(CatalogResourcePath);
		if (catalog == null)
		{
			WarnOnce("MissingCatalog", $"{nameof(AudioService)} could not load Resources/{CatalogResourcePath}.");
		}

		CreateVoicePool();
		CreateLoopVoicePool();
	}

	private void LateUpdate()
	{
		UpdateOneShotVoices();
		UpdateLoopVoices();
	}

	private void OnDestroy()
	{
		for (int i = 0; i < voices.Count; i++)
		{
			voices[i].Handle?.Invalidate(this);
		}

		for (int i = 0; i < loopVoices.Count; i++)
		{
			loopVoices[i].Handle?.Invalidate(this);
		}

		if (instance == this)
		{
			instance = null;
		}
	}

	public static void PlayAt(GameAudioCueId cueId, Vector3 worldPosition)
	{
		EnsureInstance().PlayOneShotInternal(cueId, worldPosition, null);
	}

	public static void PlayGlobal(GameAudioCueId cueId)
	{
		EnsureInstance().PlayOneShotInternal(cueId, Vector3.zero, null);
	}

	public static AudioOneShotHandle PlayGlobalControlled(GameAudioCueId cueId)
	{
		return EnsureInstance().PlayControlledOneShotInternal(cueId, Vector3.zero, null);
	}

	public static void TransitionToBulletTimeMix(bool bulletTimeActive, float transitionSeconds)
	{
		EnsureInstance().TransitionToBulletTimeMixInternal(bulletTimeActive, transitionSeconds);
	}

	public static void PlayFollowing(GameAudioCueId cueId, Transform followTarget)
	{
		if (followTarget == null)
		{
			return;
		}

		EnsureInstance().PlayOneShotInternal(cueId, followTarget.position, followTarget);
	}

	public static PreparedAudioCue PrepareOneShot(GameAudioCueId cueId)
	{
		return EnsureInstance().PrepareOneShotInternal(cueId);
	}

	public static bool PlayFollowing(PreparedAudioCue preparedCue, Transform followTarget)
	{
		if (preparedCue == null || followTarget == null)
		{
			return false;
		}

		return EnsureInstance().TryPlayPreparedOneShotInternal(
			preparedCue,
			followTarget.position,
			followTarget,
			false,
			out _);
	}

	public static AudioLoopHandle PlayFollowingLoop(GameAudioCueId cueId, Transform followTarget)
	{
		if (followTarget == null)
		{
			return null;
		}

		return EnsureInstance().PlayFollowingLoopInternal(cueId, followTarget);
	}

	private static AudioService EnsureInstance()
	{
		if (instance != null)
		{
			return instance;
		}

		AudioService existing = FindObjectOfType<AudioService>();
		if (existing != null)
		{
			instance = existing;
			return existing;
		}

		GameObject serviceObject = new GameObject(nameof(AudioService));
		return serviceObject.AddComponent<AudioService>();
	}

	private void TransitionToBulletTimeMixInternal(bool bulletTimeActive, float transitionSeconds)
	{
		if (catalog == null)
		{
			WarnOnce("MissingCatalog", $"{nameof(AudioService)} cannot transition audio snapshots because the audio catalog is missing.");
			return;
		}

		UnityEngine.Audio.AudioMixerSnapshot snapshot = bulletTimeActive
			? catalog.BulletTimeSnapshot
			: catalog.DefaultSnapshot;
		if (snapshot == null)
		{
			string snapshotName = bulletTimeActive ? "Bullet Time" : "Default";
			WarnOnce($"MissingSnapshot:{snapshotName}", $"The {snapshotName} audio snapshot is not configured.");
			return;
		}

		snapshot.TransitionTo(Mathf.Max(0f, transitionSeconds));
	}

	private void CreateVoicePool()
	{
		for (int i = voices.Count; i < VoiceCount; i++)
		{
			GameObject voiceObject = new GameObject($"Audio Voice {i + 1:00}");
			voiceObject.transform.SetParent(transform, false);

			AudioSource source = voiceObject.AddComponent<AudioSource>();
			ResetSource(source);
			voices.Add(new Voice
			{
				Source = source,
				StartedAt = float.NegativeInfinity,
				Priority = 256
			});
		}
	}

	private void CreateLoopVoicePool()
	{
		for (int i = loopVoices.Count; i < LoopVoiceCount; i++)
		{
			GameObject voiceObject = new GameObject($"Looping Audio Voice {i + 1:00}");
			voiceObject.transform.SetParent(transform, false);

			AudioSource source = voiceObject.AddComponent<AudioSource>();
			ResetSource(source);
			loopVoices.Add(new LoopVoice
			{
				Source = source
			});
		}
	}

	private void PlayOneShotInternal(GameAudioCueId cueId, Vector3 worldPosition, Transform followTarget)
	{
		PreparedAudioCue preparedCue = PrepareOneShotInternal(cueId);
		if (preparedCue != null)
		{
			TryPlayPreparedOneShotInternal(preparedCue, worldPosition, followTarget, false, out _);
		}
	}

	private AudioOneShotHandle PlayControlledOneShotInternal(
		GameAudioCueId cueId,
		Vector3 worldPosition,
		Transform followTarget)
	{
		PreparedAudioCue preparedCue = PrepareOneShotInternal(cueId);
		if (preparedCue == null)
		{
			return null;
		}

		return TryPlayPreparedOneShotInternal(
			preparedCue,
			worldPosition,
			followTarget,
			true,
			out AudioOneShotHandle handle)
			? handle
			: null;
	}

	private PreparedAudioCue PrepareOneShotInternal(GameAudioCueId cueId)
	{
		if (catalog == null)
		{
			WarnOnce("MissingCatalog", $"{nameof(AudioService)} cannot play {cueId} because the audio catalog is missing.");
			return null;
		}

		if (!catalog.TryGetCue(cueId, out AudioCueDefinition cue) || cue == null)
		{
			WarnOnce($"MissingCue:{cueId}", $"No audio cue is configured for {cueId}.");
			return null;
		}

		if (!TrySelectClip(cueId, cue, out AudioClip clip))
		{
			WarnOnce($"MissingClip:{cueId}", $"The {cueId} audio cue has no valid clips.");
			return null;
		}

		float pitch = Random.Range(cue.MinPitch, cue.MaxPitch);
		return new PreparedAudioCue(this, cue, clip, pitch);
	}

	private bool TryPlayPreparedOneShotInternal(
		PreparedAudioCue preparedCue,
		Vector3 worldPosition,
		Transform followTarget,
		bool createHandle,
		out AudioOneShotHandle handle)
	{
		handle = null;
		if (preparedCue == null
			|| preparedCue.Owner != this
			|| preparedCue.Consumed
			|| preparedCue.Definition == null
			|| preparedCue.Clip == null)
		{
			return false;
		}

		Voice voice = SelectVoice(preparedCue.Definition.Priority);
		if (voice == null)
		{
			return false;
		}

		ResetOneShotVoiceState(voice);
		ConfigureSource(voice.Source, preparedCue.Definition, preparedCue.Clip, worldPosition);
		voice.Source.pitch = preparedCue.Pitch;
		voice.Target = followTarget;
		voice.Priority = preparedCue.Definition.Priority;
		voice.StartedAt = Time.realtimeSinceStartup;
		voice.Generation++;
		if (voice.Generation == 0)
		{
			voice.Generation++;
		}

		if (createHandle)
		{
			int voiceIndex = voices.IndexOf(voice);
			handle = new AudioOneShotHandle(this, voiceIndex, voice.Generation);
			voice.Handle = handle;
		}

		preparedCue.Consumed = true;
		voice.Source.Play();
		return true;
	}

	private AudioLoopHandle PlayFollowingLoopInternal(GameAudioCueId cueId, Transform followTarget)
	{
		if (catalog == null)
		{
			WarnOnce("MissingCatalog", $"{nameof(AudioService)} cannot play {cueId} because the audio catalog is missing.");
			return null;
		}

		if (!catalog.TryGetCue(cueId, out AudioCueDefinition cue) || cue == null)
		{
			WarnOnce($"MissingCue:{cueId}", $"No audio cue is configured for {cueId}.");
			return null;
		}

		if (!cue.Loop)
		{
			WarnOnce($"NotLooping:{cueId}", $"The {cueId} audio cue is not configured to loop.");
			return null;
		}

		if (!TrySelectClip(cueId, cue, out AudioClip clip))
		{
			WarnOnce($"MissingClip:{cueId}", $"The {cueId} audio cue has no valid clips.");
			return null;
		}

		int voiceIndex = SelectLoopVoiceIndex();
		if (voiceIndex < 0)
		{
			WarnOnce("LoopVoicePoolFull", $"{nameof(AudioService)} has no free looping voices.");
			return null;
		}

		LoopVoice voice = loopVoices[voiceIndex];
		ConfigureSource(voice.Source, cue, clip, followTarget.position);
		voice.Source.loop = true;
		voice.Target = followTarget;
		voice.BaseVolume = cue.Volume;
		voice.FadeInSeconds = cue.FadeInSeconds;
		voice.FadeOutSeconds = cue.FadeOutSeconds;
		voice.Active = true;
		voice.Generation++;
		if (voice.Generation == 0)
		{
			voice.Generation++;
		}

		AudioLoopHandle handle = new AudioLoopHandle(this, voiceIndex, voice.Generation);
		voice.Handle = handle;
		voice.Source.volume = cue.FadeInSeconds > 0f ? 0f : cue.Volume;
		voice.Source.Play();
		BeginLoopFade(voice, cue.Volume, cue.FadeInSeconds, false);
		return handle;
	}

	private bool TrySelectClip(GameAudioCueId cueId, AudioCueDefinition cue, out AudioClip clip)
	{
		clip = null;
		IReadOnlyList<AudioClip> clips = cue.Clips;
		if (clips == null || clips.Count == 0)
		{
			return false;
		}

		int validClipCount = 0;
		for (int i = 0; i < clips.Count; i++)
		{
			if (clips[i] != null)
			{
				validClipCount++;
			}
		}

		if (validClipCount == 0)
		{
			return false;
		}

		bool hasLastIndex = lastClipIndices.TryGetValue(cueId, out int lastIndex);
		bool excludeLast = hasLastIndex
			&& validClipCount > 1
			&& lastIndex >= 0
			&& lastIndex < clips.Count
			&& clips[lastIndex] != null;
		int selectableCount = validClipCount - (excludeLast ? 1 : 0);
		int selectedOrdinal = Random.Range(0, selectableCount);

		for (int i = 0; i < clips.Count; i++)
		{
			if (clips[i] == null || (excludeLast && i == lastIndex))
			{
				continue;
			}

			if (selectedOrdinal == 0)
			{
				clip = clips[i];
				lastClipIndices[cueId] = i;
				return true;
			}

			selectedOrdinal--;
		}

		return false;
	}

	private Voice SelectVoice(int incomingPriority)
	{
		for (int i = 0; i < voices.Count; i++)
		{
			if (!voices[i].Source.isPlaying)
			{
				return voices[i];
			}
		}

		Voice selected = null;
		for (int i = 0; i < voices.Count; i++)
		{
			Voice candidate = voices[i];
			if (candidate.Priority < incomingPriority)
			{
				continue;
			}

			if (selected == null
				|| candidate.Priority > selected.Priority
				|| (candidate.Priority == selected.Priority && candidate.StartedAt < selected.StartedAt))
			{
				selected = candidate;
			}
		}

		if (selected != null)
		{
			selected.Source.Stop();
		}

		return selected;
	}

	private int SelectLoopVoiceIndex()
	{
		for (int i = 0; i < loopVoices.Count; i++)
		{
			if (!loopVoices[i].Active)
			{
				return i;
			}
		}

		return -1;
	}

	private void UpdateOneShotVoices()
	{
		for (int i = 0; i < voices.Count; i++)
		{
			Voice voice = voices[i];
			if (!voice.Source.isPlaying)
			{
				if (voice.Target != null || voice.Handle != null || voice.FadeDuration > 0f)
				{
					ReleaseOneShotVoice(voice);
				}
				continue;
			}

			if (voice.Target != null)
			{
				voice.Source.transform.position = voice.Target.position;
			}

			if (voice.FadeDuration <= 0f)
			{
				continue;
			}

			voice.FadeElapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(voice.FadeElapsed / voice.FadeDuration);
			voice.Source.volume = Mathf.Lerp(voice.FadeStartVolume, 0f, progress);
			if (progress >= 1f)
			{
				ReleaseOneShotVoice(voice);
			}
		}
	}

	internal bool IsOneShotHandleValid(int voiceIndex, int generation)
	{
		if (voiceIndex < 0 || voiceIndex >= voices.Count)
		{
			return false;
		}

		Voice voice = voices[voiceIndex];
		return voice.Handle != null
			&& voice.Generation == generation
			&& voice.Source.isPlaying;
	}

	internal void FadeOutOneShot(int voiceIndex, int generation, float seconds)
	{
		if (!IsOneShotHandleValid(voiceIndex, generation))
		{
			return;
		}

		Voice voice = voices[voiceIndex];
		float duration = Mathf.Max(0f, seconds);
		if (duration <= 0f)
		{
			ReleaseOneShotVoice(voice);
			return;
		}

		voice.FadeStartVolume = voice.Source.volume;
		voice.FadeDuration = duration;
		voice.FadeElapsed = 0f;
	}

	private void ResetOneShotVoiceState(Voice voice)
	{
		voice.Handle?.Invalidate(this);
		voice.Handle = null;
		voice.Target = null;
		voice.FadeStartVolume = 0f;
		voice.FadeDuration = 0f;
		voice.FadeElapsed = 0f;
	}

	private void ReleaseOneShotVoice(Voice voice)
	{
		ResetOneShotVoiceState(voice);
		voice.StartedAt = float.NegativeInfinity;
		voice.Priority = 256;
		ResetSource(voice.Source);
	}

	internal bool IsLoopHandleValid(int voiceIndex, int generation)
	{
		if (voiceIndex < 0 || voiceIndex >= loopVoices.Count)
		{
			return false;
		}

		LoopVoice voice = loopVoices[voiceIndex];
		return voice.Active && voice.Generation == generation;
	}

	internal void SetLoopHandleActive(int voiceIndex, int generation, bool active)
	{
		if (!TryGetLoopVoice(voiceIndex, generation, out LoopVoice voice))
		{
			return;
		}

		if (active)
		{
			BeginLoopFade(voice, voice.BaseVolume, voice.FadeInSeconds, false);
		}
		else
		{
			BeginLoopFade(voice, 0f, voice.FadeOutSeconds, true);
		}
	}

	internal void ReleaseLoopHandle(int voiceIndex, int generation)
	{
		if (!TryGetLoopVoice(voiceIndex, generation, out LoopVoice voice))
		{
			return;
		}

		if (voice.Target != null)
		{
			voice.Source.transform.position = voice.Target.position;
		}
		voice.Target = null;
		if (!voice.ReleaseWhenFadeCompletes || voice.FadeTargetVolume > 0f)
		{
			BeginLoopFade(voice, 0f, voice.FadeOutSeconds, true);
		}
	}

	private bool TryGetLoopVoice(int voiceIndex, int generation, out LoopVoice voice)
	{
		voice = null;
		if (!IsLoopHandleValid(voiceIndex, generation))
		{
			return false;
		}

		voice = loopVoices[voiceIndex];
		return true;
	}

	private void UpdateLoopVoices()
	{
		for (int i = 0; i < loopVoices.Count; i++)
		{
			LoopVoice voice = loopVoices[i];
			if (!voice.Active)
			{
				continue;
			}

			if (voice.Target != null)
			{
				voice.Source.transform.position = voice.Target.position;
			}
			else if (!voice.ReleaseWhenFadeCompletes)
			{
				BeginLoopFade(voice, 0f, voice.FadeOutSeconds, true);
			}

			if (!voice.Active || voice.FadeDuration <= 0f)
			{
				continue;
			}

			voice.FadeElapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(voice.FadeElapsed / voice.FadeDuration);
			voice.Source.volume = Mathf.Lerp(voice.FadeStartVolume, voice.FadeTargetVolume, progress);
			if (progress >= 1f)
			{
				voice.FadeDuration = 0f;
				if (voice.ReleaseWhenFadeCompletes && voice.FadeTargetVolume <= 0f)
				{
					ReleaseLoopVoice(voice);
				}
			}
		}
	}

	private void BeginLoopFade(LoopVoice voice, float targetVolume, float duration, bool releaseWhenComplete)
	{
		voice.FadeStartVolume = voice.Source.volume;
		voice.FadeTargetVolume = Mathf.Clamp01(targetVolume);
		voice.FadeDuration = Mathf.Max(0f, duration);
		voice.FadeElapsed = 0f;
		voice.ReleaseWhenFadeCompletes = releaseWhenComplete;

		if (voice.FadeDuration > 0f)
		{
			return;
		}

		voice.Source.volume = voice.FadeTargetVolume;
		if (releaseWhenComplete && voice.FadeTargetVolume <= 0f)
		{
			ReleaseLoopVoice(voice);
		}
	}

	private void ReleaseLoopVoice(LoopVoice voice)
	{
		voice.Handle?.Invalidate(this);
		voice.Handle = null;
		voice.Target = null;
		voice.Active = false;
		voice.BaseVolume = 0f;
		voice.FadeDuration = 0f;
		voice.FadeElapsed = 0f;
		voice.ReleaseWhenFadeCompletes = false;
		ResetSource(voice.Source);
	}

	private static void ConfigureSource(AudioSource source, AudioCueDefinition cue, AudioClip clip, Vector3 worldPosition)
	{
		ResetSource(source);
		source.transform.position = worldPosition;
		source.clip = clip;
		source.outputAudioMixerGroup = cue.OutputMixerGroup;
		source.volume = cue.Volume;
		source.pitch = Random.Range(cue.MinPitch, cue.MaxPitch);
		source.spatialBlend = cue.SpatialBlend;
		source.minDistance = cue.MinDistance;
		source.maxDistance = cue.MaxDistance;
		source.rolloffMode = cue.RolloffMode;
		source.priority = cue.Priority;
	}

	private static void ResetSource(AudioSource source)
	{
		source.Stop();
		source.clip = null;
		source.outputAudioMixerGroup = null;
		source.playOnAwake = false;
		source.loop = false;
		source.bypassEffects = false;
		source.bypassListenerEffects = false;
		source.bypassReverbZones = false;
		source.volume = 1f;
		source.pitch = 1f;
		source.panStereo = 0f;
		source.spatialBlend = 1f;
		source.reverbZoneMix = 1f;
		source.dopplerLevel = 0f;
		source.spread = 0f;
		source.priority = 128;
		source.mute = false;
	}

	private void WarnOnce(string key, string message)
	{
		if (warnings.Add(key))
		{
			Debug.LogWarning(message, this);
		}
	}
}
