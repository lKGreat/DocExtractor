using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DocExtractor.Core.Exceptions;
using Newtonsoft.Json;

namespace DocExtractor.ML.ModelRegistry
{
    /// <summary>
    /// 模型版本注册表：负责版本归档、当前版本切换与退化保护。
    /// </summary>
    public class ModelRegistryStore
    {
        private readonly string _modelsDir;
        private readonly string _versionsDir;
        private readonly string _registryPath;

        public ModelRegistryStore(string modelsDir)
        {
            _modelsDir = modelsDir;
            _versionsDir = Path.Combine(modelsDir, "versions");
            _registryPath = Path.Combine(modelsDir, "model_registry.json");
        }

        public ModelVersionInfo PublishVersion(
            string modelName,
            string sourceModelPath,
            double accuracy,
            int samples,
            string parameters,
            bool blockOnRegression = true,
            double regressionThreshold = 0.03)
        {
            Directory.CreateDirectory(_modelsDir);
            Directory.CreateDirectory(_versionsDir);

            if (!File.Exists(sourceModelPath))
            {
                throw new ModelException(
                    $"模型文件不存在：{sourceModelPath}",
                    modelName,
                    sourceModelPath);
            }

            var registry = LoadRegistry();
            if (!registry.TryGetValue(modelName, out var entry))
            {
                entry = new ModelRegistryEntry();
                registry[modelName] = entry;
            }

            if (blockOnRegression)
            {
                var current = entry.Versions.FirstOrDefault(v => v.Version == entry.Current);
                if (current != null && current.Accuracy > 0 &&
                    accuracy < current.Accuracy - regressionThreshold)
                {
                    throw new ModelException(
                        $"模型「{modelName}」新版本准确率 {accuracy:P2} 低于当前版本 {current.Accuracy:P2}，下降超过 {regressionThreshold:P0}，已阻止覆盖",
                        modelName,
                        sourceModelPath);
                }
            }

            int nextVersionNumber = GetNextVersionNumber(entry);
            string version = "v" + nextVersionNumber.ToString(CultureInfo.InvariantCulture);
            string accuracyText = (accuracy * 100d).ToString("F1", CultureInfo.InvariantCulture) + "pct";
            string versionFileName = $"{modelName}_{version}_{accuracyText}.zip";
            string versionTargetPath = Path.Combine(_versionsDir, versionFileName);

            File.Copy(sourceModelPath, versionTargetPath, overwrite: true);

            string currentPath = Path.Combine(_modelsDir, modelName + ".zip");
            File.Copy(sourceModelPath, currentPath, overwrite: true);

            var info = new ModelVersionInfo
            {
                Version = version,
                Accuracy = accuracy,
                Samples = samples,
                TrainedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Parameters = parameters ?? string.Empty,
                FileName = versionFileName
            };

            entry.Versions.Add(info);
            entry.Current = version;
            SaveRegistry(registry);
            return info;
        }

        public IReadOnlyList<ModelVersionInfo> GetVersions(string modelName)
        {
            var registry = LoadRegistry();
            if (!registry.TryGetValue(modelName, out var entry))
                return Array.Empty<ModelVersionInfo>();
            return entry.Versions
                .OrderByDescending(v => v.TrainedAt)
                .ToList();
        }

        public string? GetCurrentVersion(string modelName)
        {
            var registry = LoadRegistry();
            if (!registry.TryGetValue(modelName, out var entry))
                return null;
            return entry.Current;
        }

        public bool Rollback(string modelName, string version)
        {
            var registry = LoadRegistry();
            if (!registry.TryGetValue(modelName, out var entry))
                return false;

            var target = entry.Versions.FirstOrDefault(v => v.Version == version);
            if (target == null) return false;

            string source = Path.Combine(_versionsDir, target.FileName);
            if (!File.Exists(source))
                throw new ModelException(
                    $"回滚失败，版本文件不存在：{source}",
                    modelName,
                    source);

            string currentPath = Path.Combine(_modelsDir, modelName + ".zip");
            File.Copy(source, currentPath, overwrite: true);

            entry.Current = version;
            SaveRegistry(registry);
            return true;
        }

        private int GetNextVersionNumber(ModelRegistryEntry entry)
        {
            int max = 0;
            foreach (var item in entry.Versions)
            {
                if (string.IsNullOrWhiteSpace(item.Version)) continue;
                if (!item.Version.StartsWith("v", StringComparison.OrdinalIgnoreCase)) continue;
                string numberPart = item.Version.Substring(1);
                if (int.TryParse(numberPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out int num))
                    max = Math.Max(max, num);
            }
            return max + 1;
        }

        private Dictionary<string, ModelRegistryEntry> LoadRegistry()
        {
            if (!File.Exists(_registryPath))
                return new Dictionary<string, ModelRegistryEntry>(StringComparer.OrdinalIgnoreCase);

            string json = File.ReadAllText(_registryPath);
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, ModelRegistryEntry>(StringComparer.OrdinalIgnoreCase);

            var data = JsonConvert.DeserializeObject<Dictionary<string, ModelRegistryEntry>>(json);
            return data ?? new Dictionary<string, ModelRegistryEntry>(StringComparer.OrdinalIgnoreCase);
        }

        private void SaveRegistry(Dictionary<string, ModelRegistryEntry> registry)
        {
            string json = JsonConvert.SerializeObject(registry, Formatting.Indented);
            File.WriteAllText(_registryPath, json);
        }
    }
}
