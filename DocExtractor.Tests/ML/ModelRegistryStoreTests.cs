using System;
using System.IO;
using DocExtractor.Core.Exceptions;
using DocExtractor.ML.ModelRegistry;
using Xunit;

namespace DocExtractor.Tests.ML
{
    public class ModelRegistryStoreTests
    {
        [Fact]
        public void PublishVersion_ShouldCreateCurrentAndVersionFile()
        {
            string modelsDir = CreateTempDir();
            try
            {
                string source = Path.Combine(modelsDir, "temp.zip");
                File.WriteAllText(source, "fake-model");

                var store = new ModelRegistryStore(modelsDir);
                var info = store.PublishVersion("column_classifier", source, 0.952, 120, "Fine");

                Assert.Equal("v1", info.Version);
                Assert.True(File.Exists(Path.Combine(modelsDir, "column_classifier.zip")));
                Assert.True(File.Exists(Path.Combine(modelsDir, "versions", info.FileName)));
                Assert.True(File.Exists(Path.Combine(modelsDir, "model_registry.json")));
            }
            finally
            {
                SafeDeleteDir(modelsDir);
            }
        }

        [Fact]
        public void PublishVersion_ShouldBlockRegression_WhenDropExceedsThreshold()
        {
            string modelsDir = CreateTempDir();
            try
            {
                var store = new ModelRegistryStore(modelsDir);

                string source1 = Path.Combine(modelsDir, "temp1.zip");
                File.WriteAllText(source1, "model-1");
                store.PublishVersion("column_classifier", source1, 0.95, 100, "Standard");

                string source2 = Path.Combine(modelsDir, "temp2.zip");
                File.WriteAllText(source2, "model-2");

                Assert.Throws<ModelException>(() =>
                    store.PublishVersion("column_classifier", source2, 0.90, 110, "Fine"));
            }
            finally
            {
                SafeDeleteDir(modelsDir);
            }
        }

        private static string CreateTempDir()
        {
            string dir = Path.Combine(Path.GetTempPath(), "docextractor-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            return dir;
        }

        private static void SafeDeleteDir(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch
            {
            }
        }
    }
}
