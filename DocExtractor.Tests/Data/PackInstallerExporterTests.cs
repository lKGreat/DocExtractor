using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Core.Models;
using DocExtractor.Core.Packs;
using DocExtractor.Data.Packs;
using DocExtractor.Data.Repositories;
using Xunit;

namespace DocExtractor.Tests.Data
{
    public class PackInstallerExporterTests
    {
        [Fact]
        public void ExportAndInstall_ShouldCreatePackAndImportConfig()
        {
            string root = CreateTempDir();
            try
            {
                string packPath = Path.Combine(root, "test.dxpack");
                string dbPath = Path.Combine(root, "docextractor.db");
                string packsRoot = Path.Combine(root, "packs");

                var config = new ExtractionConfig
                {
                    ConfigName = "测试配置",
                    Fields = new List<FieldDefinition>
                    {
                        new FieldDefinition { FieldName = "A", DisplayName = "字段A" }
                    }
                };

                var exporter = new PackExporter();
                exporter.Export(packPath, new PackManifest
                {
                    PackId = "test-pack",
                    Name = "测试包",
                    Version = "1.0.0",
                    Author = "UnitTest",
                    Domain = "test"
                }, new[] { config });

                Assert.True(File.Exists(packPath));

                var installer = new PackInstaller(dbPath, packsRoot);
                var result = installer.Install(packPath);

                Assert.Equal("test-pack", result.Manifest.PackId);
                Assert.Equal(1, result.ImportedConfigCount);
                Assert.True(Directory.Exists(Path.Combine(packsRoot, "test-pack")));

                using var repo = new ExtractionConfigRepository(dbPath);
                var all = repo.GetAll();
                Assert.Contains(all, c => c.Name.Contains("测试配置"));
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "docextractor-pack-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void SafeDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
