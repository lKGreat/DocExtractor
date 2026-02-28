using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using DocExtractor.Core.Models;
using DocExtractor.Core.Packs;
using DocExtractor.Data.Repositories;
using Newtonsoft.Json;

namespace DocExtractor.Data.Packs
{
    /// <summary>
    /// 领域配置包安装器：安装 .dxpack 到本地 packs 目录并合并配置。
    /// </summary>
    public class PackInstaller
    {
        private readonly string _dbPath;
        private readonly string _packsRoot;

        public PackInstaller(string dbPath, string packsRoot)
        {
            _dbPath = dbPath;
            _packsRoot = packsRoot;
        }

        public PackInstallResult Install(string packFilePath, bool overwriteConfigOnConflict = false)
        {
            if (!File.Exists(packFilePath))
                throw new FileNotFoundException("配置包文件不存在", packFilePath);

            string tempDir = Path.Combine(Path.GetTempPath(), "dxpack-install-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                ZipFile.ExtractToDirectory(packFilePath, tempDir);

                string manifestPath = Path.Combine(tempDir, "manifest.json");
                if (!File.Exists(manifestPath))
                    throw new InvalidOperationException("配置包缺少 manifest.json");

                var manifest = JsonConvert.DeserializeObject<PackManifest>(File.ReadAllText(manifestPath))
                               ?? throw new InvalidOperationException("manifest.json 无法解析");
                if (string.IsNullOrWhiteSpace(manifest.PackId))
                    throw new InvalidOperationException("manifest.json 缺少 packId");

                string packInstallDir = Path.Combine(_packsRoot, manifest.PackId);
                if (Directory.Exists(packInstallDir))
                    Directory.Delete(packInstallDir, recursive: true);
                CopyDirectory(tempDir, packInstallDir);

                int importedConfigs = ImportConfigs(tempDir, manifest.PackId, overwriteConfigOnConflict);

                return new PackInstallResult
                {
                    Manifest = manifest,
                    InstallDirectory = packInstallDir,
                    ImportedConfigCount = importedConfigs
                };
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
                    // 临时目录清理失败不影响主流程
                }
            }
        }

        private int ImportConfigs(string extractedRoot, string packId, bool overwriteConfigOnConflict)
        {
            string configsDir = Path.Combine(extractedRoot, "configs");
            if (!Directory.Exists(configsDir)) return 0;

            var configFiles = Directory.GetFiles(configsDir, "*.json", SearchOption.TopDirectoryOnly);
            if (configFiles.Length == 0) return 0;

            int imported = 0;
            using var repo = new ExtractionConfigRepository(_dbPath);
            var existingNames = new HashSet<string>(repo.GetAll().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var file in configFiles)
            {
                var config = JsonConvert.DeserializeObject<ExtractionConfig>(File.ReadAllText(file));
                if (config == null || string.IsNullOrWhiteSpace(config.ConfigName))
                    continue;

                if (existingNames.Contains(config.ConfigName) && !overwriteConfigOnConflict)
                    config.ConfigName = $"{config.ConfigName} ({packId})";

                repo.Save(config);
                existingNames.Add(config.ConfigName);
                imported++;
            }

            return imported;
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(targetDir, fileName), overwrite: true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                var name = Path.GetFileName(dir);
                CopyDirectory(dir, Path.Combine(targetDir, name));
            }
        }
    }

    public class PackInstallResult
    {
        public PackManifest Manifest { get; set; } = new PackManifest();
        public string InstallDirectory { get; set; } = string.Empty;
        public int ImportedConfigCount { get; set; }
    }
}
