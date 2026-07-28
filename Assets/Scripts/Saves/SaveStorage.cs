using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace SLG.Saves
{
    public interface ISaveStorage
    {
        bool Exists(string fileName);
        bool TryReadText(string fileName, out string text, out string error);
        bool TryWriteTextAtomic(string fileName, string text, out string error);
        bool TryDelete(string fileName, out string error);
        IReadOnlyList<string> ListFiles();
    }

    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string rootPath;

        public FileSaveStorage(string rootPath)
        {
            this.rootPath = rootPath;
            Directory.CreateDirectory(rootPath);
        }

        public static FileSaveStorage CreateProduction()
        {
            return new FileSaveStorage(Path.Combine(Application.persistentDataPath, "Saves"));
        }

        public bool Exists(string fileName) => File.Exists(PathFor(fileName));

        public bool TryReadText(string fileName, out string text, out string error)
        {
            try
            {
                text = File.ReadAllText(PathFor(fileName), Encoding.UTF8);
                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                text = string.Empty;
                error = ex.Message;
                return false;
            }
        }

        public bool TryWriteTextAtomic(string fileName, string text, out string error)
        {
            string finalPath = PathFor(fileName);
            string tempPath = finalPath + ".tmp";
            try
            {
                Directory.CreateDirectory(rootPath);
                File.WriteAllText(tempPath, text, new UTF8Encoding(false));
                if (File.Exists(finalPath))
                {
                    File.Replace(tempPath, finalPath, null);
                }
                else
                {
                    File.Move(tempPath, finalPath);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }

                error = ex.Message;
                return false;
            }
        }

        public bool TryDelete(string fileName, out string error)
        {
            try
            {
                string path = PathFor(fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public IReadOnlyList<string> ListFiles()
        {
            if (!Directory.Exists(rootPath))
            {
                return Array.Empty<string>();
            }

            string[] paths = Directory.GetFiles(rootPath, "*.json");
            string[] names = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                names[i] = Path.GetFileName(paths[i]);
            }

            return names;
        }

        private string PathFor(string fileName) => Path.Combine(rootPath, SavePathUtility.SanitizeFileName(fileName));
    }

    public sealed class InMemorySaveStorage : ISaveStorage
    {
        private readonly Dictionary<string, string> files = new Dictionary<string, string>();

        public bool Exists(string fileName) => files.ContainsKey(SavePathUtility.SanitizeFileName(fileName));

        public bool TryReadText(string fileName, out string text, out string error)
        {
            if (files.TryGetValue(SavePathUtility.SanitizeFileName(fileName), out text))
            {
                error = string.Empty;
                return true;
            }

            text = string.Empty;
            error = "File not found.";
            return false;
        }

        public bool TryWriteTextAtomic(string fileName, string text, out string error)
        {
            files[SavePathUtility.SanitizeFileName(fileName)] = text;
            error = string.Empty;
            return true;
        }

        public bool TryDelete(string fileName, out string error)
        {
            files.Remove(SavePathUtility.SanitizeFileName(fileName));
            error = string.Empty;
            return true;
        }

        public IReadOnlyList<string> ListFiles() => new List<string>(files.Keys);

        public void WriteRaw(string fileName, string text) => files[SavePathUtility.SanitizeFileName(fileName)] = text;

        public void ClearAll()
        {
            files.Clear();
        }
    }

    public static class SavePathUtility
    {
        public static string CampaignSlotFileName(int slot)
        {
            if (slot < 1 || slot > SaveConstants.ManualCampaignSlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), "Manual campaign slot must be 1-5.");
            }

            return $"campaign-slot-{slot:00}.json";
        }

        public static string SanitizeFileName(string fileName)
        {
            string name = Path.GetFileName(fileName ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "invalid.json" : name;
        }
    }
}
