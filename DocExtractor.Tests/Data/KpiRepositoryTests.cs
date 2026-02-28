using System;
using System.IO;
using DocExtractor.Data.Repositories;
using Xunit;

namespace DocExtractor.Tests.Data
{
    public class KpiRepositoryTests
    {
        [Fact]
        public void GetSnapshot_ShouldReturnDefaultMetrics_WhenDataIsEmpty()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "db.sqlite");
                string modelsDir = Path.Combine(root, "models");
                Directory.CreateDirectory(modelsDir);

                // 初始化基础表
                using (var repo = new TrainingDataRepository(dbPath)) { }
                using (var repo = new GroupKnowledgeRepository(dbPath)) { }

                var kpiRepo = new KpiRepository(dbPath, modelsDir);
                var snapshot = kpiRepo.GetSnapshot();

                Assert.Equal(0, snapshot.ColumnSamples);
                Assert.Equal(0, snapshot.KnowledgeCount);
                Assert.False(snapshot.ColumnModelExists);
                Assert.NotEmpty(snapshot.Suggestions);
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "docextractor-kpi-tests-" + Guid.NewGuid().ToString("N"));
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
