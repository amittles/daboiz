using UnityEngine;

namespace Daboiz.Core
{
    /// <summary>A lobby slot (human or bot). Persists across minigames; Actors are per-round bodies.</summary>
    public class Participant
    {
        public int Index;
        public string Name;
        public Color Color;
        public bool IsLocal;
        public bool IsBot { get { return !IsLocal; } }
        public float BotSkill = 0.5f;
        public int PartyPoints;

        public Participant(int index, string name, Color color, bool isLocal, float skill)
        {
            Index = index; Name = name; Color = color; IsLocal = isLocal; BotSkill = skill;
        }
    }

    /// <summary>One line of a minigame's final ranking. Equal Place = tie.</summary>
    public class RankEntry
    {
        public Participant Participant;
        public int Place; // 1 = winner
        public string Detail;

        public RankEntry(Participant p, int place, string detail)
        {
            Participant = p; Place = place; Detail = detail;
        }
    }

    [System.Serializable]
    public class GameSettings
    {
        public int BotCount = 5;
        public bool AutoBhop = true;
        public float MouseSensitivity = 2f;
        public float BaseFov = 95f;
        public bool HeadBob = false;
        public bool InvertY = false;
        public float Volume = 0.6f;

        public void Load()
        {
            BotCount = PlayerPrefs.GetInt("daboiz.bots", BotCount);
            AutoBhop = PlayerPrefs.GetInt("daboiz.autobhop", AutoBhop ? 1 : 0) == 1;
            MouseSensitivity = PlayerPrefs.GetFloat("daboiz.sens", MouseSensitivity);
            BaseFov = PlayerPrefs.GetFloat("daboiz.fov", BaseFov);
            HeadBob = PlayerPrefs.GetInt("daboiz.headbob", HeadBob ? 1 : 0) == 1;
            InvertY = PlayerPrefs.GetInt("daboiz.inverty", InvertY ? 1 : 0) == 1;
            Volume = PlayerPrefs.GetFloat("daboiz.volume", Volume);
        }

        public void Save()
        {
            PlayerPrefs.SetInt("daboiz.bots", BotCount);
            PlayerPrefs.SetInt("daboiz.autobhop", AutoBhop ? 1 : 0);
            PlayerPrefs.SetFloat("daboiz.sens", MouseSensitivity);
            PlayerPrefs.SetFloat("daboiz.fov", BaseFov);
            PlayerPrefs.SetInt("daboiz.headbob", HeadBob ? 1 : 0);
            PlayerPrefs.SetInt("daboiz.inverty", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat("daboiz.volume", Volume);
            PlayerPrefs.Save();
        }
    }
}
