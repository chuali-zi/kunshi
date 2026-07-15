using System;
using System.IO;
using ConGame.Domain.Persistence;
using UnityEngine;

namespace ConGame.Runtime.Persistence
{

    public sealed class JsonSaveStore
    {
        private const string FileName = "save.json";

        public string SavePath => Path.Combine(Application.persistentDataPath, FileName);

        private string BackupPath => SavePath + ".bak";
        private string TempPath => SavePath + ".tmp";

        public PersistentProgressV1 Load()
        {
            PersistentProgressV1 progress = TryRead(SavePath) ?? TryRead(BackupPath) ?? PersistentProgressV1.CreateNew();
            progress.Normalize();
            return progress;
        }

        public void SaveAtomic(PersistentProgressV1 progress)
        {
            if (progress == null)
            {
                throw new ArgumentNullException(nameof(progress));
            }

            progress.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath) ?? Application.persistentDataPath);
            File.WriteAllText(TempPath, JsonUtility.ToJson(progress, true));

            try
            {
                if (File.Exists(SavePath))
                {
                    DeleteIfExists(BackupPath);
                    File.Replace(TempPath, SavePath, BackupPath, true);
                }
                else
                {
                    File.Move(TempPath, SavePath);
                }
            }
            catch (PlatformNotSupportedException)
            {
                FallbackMove();
            }
            catch (IOException)
            {
                FallbackMove();
            }
        }

        public void Delete()
        {
            DeleteIfExists(SavePath);
            DeleteIfExists(BackupPath);
            DeleteIfExists(TempPath);
        }

        private PersistentProgressV1 TryRead(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            try
            {
                return JsonUtility.FromJson<PersistentProgressV1>(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read ConGame save '{path}': {exception.Message}");
                return null;
            }
        }

        private void FallbackMove()
        {
            if (File.Exists(SavePath))
            {
                File.Copy(SavePath, BackupPath, true);
            }

            DeleteIfExists(SavePath);
            File.Move(TempPath, SavePath);
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
