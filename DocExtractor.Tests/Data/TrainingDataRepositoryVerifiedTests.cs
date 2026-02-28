using System;
using System.IO;
using DocExtractor.Data.Repositories;
using Xunit;

namespace DocExtractor.Tests.Data
{
    public class TrainingDataRepositoryVerifiedTests
    {
        [Fact]
        public void GetVerifiedColumnSampleCount_ShouldReturnCountBySource()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "db.sqlite");
                using (var repo = new TrainingDataRepository(dbPath))
                {
                    repo.AddColumnSample("列A", "FieldA", "ManualPreview", isVerified: true);
                    repo.AddColumnSample("列B", "FieldB", "ManualPreview", isVerified: true);
                    repo.AddColumnSample("列C", "FieldC", "ImportCsv", isVerified: false);
                }

                using (var repo = new TrainingDataRepository(dbPath))
                {
                    Assert.Equal(2, repo.GetVerifiedColumnSampleCount("ManualPreview"));
                    Assert.Equal(2, repo.GetVerifiedColumnSampleCount());
                }
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "docextractor-verified-tests-" + Guid.NewGuid().ToString("N"));
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
