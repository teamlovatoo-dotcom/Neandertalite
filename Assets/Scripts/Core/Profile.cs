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
    }
}
