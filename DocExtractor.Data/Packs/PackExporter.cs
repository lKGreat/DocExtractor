using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using DocExtractor.Core.Models;
using DocExtractor.Core.Packs;
using Newtonsoft.Json;

namespace DocExtractor.Data.Packs
{
    /// <summary>
    /// 领域配置包导出器：将配置和可选模型打包为 .dxpack。
    /// </summary>
    public class PackExporter
    {
        public void Export(
            string outputPackPath,
            PackManifest manifest,
            IReadOnlyList<ExtractionConfig> configs,
            string? pretrainedModelPath = null,
            string? templatePath = null)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "dxpack-export-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                manifest.ConfigCount = configs.Count;
                manifest.HasPretrainedModel = !string.IsNullOrWhiteSpace(pretrainedModelPath)
                                              && File.Exists(pretrainedModelPath);

                File.WriteAllText(
                    Path.Combine(tempDir, "manifest.json"),
                    JsonConvert.SerializeObject(manifest, Formatting.Indented));

                var configsDir = Path.Combine(tempDir, "configs");
                Directory.CreateDirectory(configsDir);
                for (int i = 0; i < configs.Count; i++)
                {
                    string configName = SanitizeFileName(configs[i].ConfigName);
                    string configPath = Path.Combine(configsDir, $"{i + 1:00}_{configName}.json");
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(configs[i], Formatting.Indented));
                }

                if (!string.IsNullOrWhiteSpace(pretrainedModelPath) && File.Exists(pretrainedModelPath))
                {
                    var modelsDir = Path.Combine(tempDir, "models");
                    Directory.CreateDirectory(modelsDir);
                    File.Copy(pretrainedModelPath, Path.Combine(modelsDir, "column_classifier.zip"), overwrite: true);
                }

                if (!string.IsNullOrWhiteSpace(templatePath) && File.Exists(templatePath))
                {
                    var templatesDir = Path.Combine(tempDir, "templates");
                    Directory.CreateDirectory(templatesDir);
                    File.Copy(templatePath, Path.Combine(templatesDir, Path.GetFileName(templatePath)), overwrite: true);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(outputPackPath) ?? ".");
                if (File.Exists(outputPackPath))
                    File.Delete(outputPackPath);

                ZipFile.CreateFromDirectory(tempDir, outputPackPath, CompressionLevel.Optimal, includeBaseDirectory: false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(tempDir))
                        Directory.Delete(tempDir, recursive: true);
                }
                catch
                {
                    // 忽略临时目录清理失败
                }
            }
        }

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "config";
            string output = input;
            foreach (char c in Path.GetInvalidFileNameChars())
                output = output.Replace(c, '_');
            return output;
        }
    }
}
