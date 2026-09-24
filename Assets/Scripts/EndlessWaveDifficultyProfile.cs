using UnityEngine;

[CreateAssetMenu(
    fileName = "EndlessWaveDifficulty",
    menuName = "Game/Endless Wave Difficulty",
    order = 0)]
public sealed class EndlessWaveDifficultyProfile : ScriptableObject
{
    private const float MinimumSpawnInterval = 0.01f;

    [Header("Mode")]
    [SerializeField] private bool limitWaveCount = true;
    [SerializeField, Min(1)] private int numberOfWaves = 3;

    [Header("Capacity")]
    [SerializeField, Min(1)] private int maxActiveEnemies = 16;
    [SerializeField, Min(1)] private int waveSize = 4;

    [Header("Wave Timing")]
    [SerializeField, Min(MinimumSpawnInterval)] private float minWaveInterval = 18f;
    [SerializeField, Min(MinimumSpawnInterval)] private float maxWaveInterval = 23f;

    [Header("Trickle Timing")]
    [SerializeField, Min(MinimumSpawnInterval)] private float minTrickleInterval = 6f;
    [SerializeField, Min(MinimumSpawnInterval)] private float maxTrickleInterval = 10f;

    public bool LimitWaveCount => limitWaveCount;
    public int NumberOfWaves => numberOfWaves;
    public int MaxActiveEnemies => maxActiveEnemies;
    public int WaveSize => waveSize;
    public float MinWaveInterval => minWaveInterval;
    public float MaxWaveInterval => maxWaveInterval;
    public float MinTrickleInterval => minTrickleInterval;
    public float MaxTrickleInterval => maxTrickleInterval;

    private void OnValidate()
    {
        numberOfWaves = Mathf.Max(1, numberOfWaves);
        maxActiveEnemies = Mathf.Max(1, maxActiveEnemies);
        waveSize = Mathf.Clamp(waveSize, 1, maxActiveEnemies);
        NormalizeIntervalRange(ref minWaveInterval, ref maxWaveInterval);
        NormalizeIntervalRange(ref minTrickleInterval, ref maxTrickleInterval);
    }

    private static void NormalizeIntervalRange(ref float minimum, ref float maximum)
    {
        minimum = Mathf.Max(MinimumSpawnInterval, minimum);
        maximum = Mathf.Max(MinimumSpawnInterval, maximum);
        if (minimum > maximum)
        {
            float previousMinimum = minimum;
            minimum = maximum;
            maximum = previousMinimum;
        }
    }
}
