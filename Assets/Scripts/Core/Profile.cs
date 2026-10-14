using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Neanderthal
{
    /// <summary>
    /// Persistent player profile: coins and owned blueprints carry over between Survival and Duels.
    /// Stored as JSON in Application.persistentDataPath.
    /// </summary>
    [Serializable]
    public class Profile
    {
        public int Coins = 150;
        public List<string> Blueprints = new List<string>();
        public float MouseSensitivity = 2f;
        public float Fov = 80f;
        public float Volume = 0.8f;
        public int DuelWins;
        public int DuelLosses;
        public bool ChapterOneComplete;
        public List<ServerRecord> ServerHistory = new List<ServerRecord>();

        const int MaxServerHistory = 20;

        static Profile current;
        static string PathOnDisk => System.IO.Path.Combine(Application.persistentDataPath, "neanderthal_profile.json");

        public static Profile Current
        {
            get
            {
                if (current == null) Load();
                return current;
            }
        }

        public static event Action Changed;

        public bool HasBlueprint(string id) => !string.IsNullOrEmpty(id) && Blueprints.Contains(id);

        public void AddBlueprint(string id)
        {
            if (!Blueprints.Contains(id)) Blueprints.Add(id);
            Save();
        }

        public bool Spend(int amount)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            Save();
            return true;
        }

        public void Earn(int amount)
        {
            Coins += amount;
            Save();
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(PathOnDisk))
                    current = JsonUtility.FromJson<Profile>(File.ReadAllText(PathOnDisk));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Profile load failed, starting fresh: " + e.Message);
            }
            if (current == null) current = new Profile();
            current.Apply();
        }

        public void Apply()
        {
            GameInput.MouseSensitivity = MouseSensitivity;
            Sfx.Volume = Volume;
            AudioListener.volume = 1f;
        }

        public void Save()
        {
            Apply();
            try
            {
                File.WriteAllText(PathOnDisk, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("Profile save failed: " + e.Message);
            }
            Changed?.Invoke();
        }

        public static void Reset()
        {
            current = new Profile();
            current.Save();
        }

        /// <summary>Puts the server at the top of the history (replacing an entry with the same name).</summary>
        public void RememberServer(ServerRecord server)
        {
            server.LastPlayed = DateTime.Now.ToString("dd.MM.yyyy HH:mm");
            if (string.IsNullOrEmpty(server.Created)) server.Created = server.LastPlayed;
            ServerHistory.RemoveAll(s => s.Name == server.Name);
            ServerHistory.Insert(0, server);
            if (ServerHistory.Count > MaxServerHistory) ServerHistory.RemoveRange(MaxServerHistory, ServerHistory.Count - MaxServerHistory);
            Save();
        }

        public void ForgetServer(ServerRecord server)
        {
            ServerHistory.Remove(server);
            Save();
        }
    }

    /// <summary>A server the player created, kept in the profile for "Історія серверів".</summary>
    [Serializable]
    public class ServerRecord
    {
        public string Name = "Печера неандертальців";
        public int Map = (int)ArenaMap.Catacombs;
        public int Opponents = 1;
        public int MaxPlayers = 2;
        public int ScoreToWin = 5;
        public int Port = 27015;
        public string Created;
        public string LastPlayed;

        public ServerRecord Copy() => (ServerRecord)MemberwiseClone();
    }
}
