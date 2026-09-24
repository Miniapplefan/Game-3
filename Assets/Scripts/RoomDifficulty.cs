using UnityEngine;

public enum RoomDifficulty
{
    Easy = 0,
    Medium = 1,
    Hard = 2
}

public static class RoomDifficultySelection
{
    private const string PlayerPrefsKey = "RoomDifficulty";

    public static RoomDifficulty Get()
    {
        int savedValue = PlayerPrefs.GetInt(PlayerPrefsKey, (int)RoomDifficulty.Easy);
        if (savedValue < (int)RoomDifficulty.Easy || savedValue > (int)RoomDifficulty.Hard)
        {
            return RoomDifficulty.Easy;
        }

        return (RoomDifficulty)savedValue;
    }

    public static void Set(RoomDifficulty difficulty)
    {
        int difficultyValue = (int)difficulty;
        if (difficultyValue < (int)RoomDifficulty.Easy
            || difficultyValue > (int)RoomDifficulty.Hard)
        {
            difficulty = RoomDifficulty.Easy;
        }

        PlayerPrefs.SetInt(PlayerPrefsKey, (int)difficulty);
        PlayerPrefs.Save();
    }
}
