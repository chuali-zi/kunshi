using System;
using System.IO;
using UnityEngine;

namespace ConGame.Runtime.Settings
{
    public sealed class GameSettingsStore
    {
        private const string FileName = "settings.json";

        public GameSettingsStore(string path = null)
        {
            SettingsPath = string.IsNullOrWhiteSpace(path)
                ? Path.Combine(Application.persistentDataPath, FileName)
                : path;
        }

        public string SettingsPath { get; }

        private string BackupPath => SettingsPath + ".bak";
        private string TempPath => SettingsPath + ".tmp";

        public GameSettingsV1 Load()
        {
            GameSettingsV1 settings = TryRead(SettingsPath) ?? TryRead(BackupPath) ?? new GameSettingsV1();
            settings.Normalize();
            Apply(settings);
            return settings;
        }

        public bool SaveAtomic(GameSettingsV1 settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Normalize();
            Apply(settings);

            try
            {
                string directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(TempPath, JsonUtility.ToJson(settings, true));
                if (File.Exists(SettingsPath))
                {
                    File.Copy(SettingsPath, BackupPath, true);
                    File.Delete(SettingsPath);
                }

                File.Move(TempPath, SettingsPath);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save ConGame settings: {exception.Message}");
                TryDelete(TempPath);
                return false;
            }
        }

        public static void Apply(GameSettingsV1 settings)
        {
            if (settings != null)
            {
                AudioListener.volume = settings.masterVolume;
            }
        }

        private static GameSettingsV1 TryRead(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<GameSettingsV1>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read ConGame settings '{path}': {exception.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // A failed cleanup must not hide the original save failure.
            }
        }
    }
}
