using System;
using System.Collections.Generic;
using System.IO;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.EntityExtractor;
using Xunit;

namespace DocExtractor.Tests.Data
{
    public class ScenarioModePersistenceTests
    {
        [Fact]
        public void Repository_ShouldPersistScenarioModesAndTemplate()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "docextractor.db");

                int scenarioId;
                using (var repo = new ActiveLearningRepository(dbPath))
                {
                    scenarioId = repo.AddScenario(new NlpScenario
                    {
                        Name = "ModeScenario",
                        Description = "mode test",
                        EntityTypes = new List<string> { "KeyInfo", "Value" },
                        EnabledModes = new List<AnnotationMode> { AnnotationMode.SpanEntity, AnnotationMode.KvSchema, AnnotationMode.EnumBitfield },
                        TemplateConfigJson = "{\"name\":\"test-template\"}",
                        IsBuiltIn = false
                    });
                }

                using (var repo = new ActiveLearningRepository(dbPath))
                {
                    var scenario = repo.GetScenarioById(scenarioId);
                    Assert.NotNull(scenario);
                    Assert.Contains(AnnotationMode.KvSchema, scenario!.EnabledModes);
                    Assert.Contains(AnnotationMode.EnumBitfield, scenario.EnabledModes);
                    Assert.Contains("test-template", scenario.TemplateConfigJson);
                }
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        [Fact]
        public void Engine_ShouldSaveStructuredAnnotations()
        {
            string root = CreateTempDir();
            try
            {
                string dbPath = Path.Combine(root, "docextractor.db");
                string modelsDir = Path.Combine(root, "models");
                Directory.CreateDirectory(modelsDir);

                int scenarioId;
                using (var repo = new ActiveLearningRepository(dbPath))
                {
                    scenarioId = repo.AddScenario(new NlpScenario
                    {
                        Name = "StructuredScenario",
                        Description = "structured test",
                        EntityTypes = new List<string> { "KeyInfo" },
                        EnabledModes = new List<AnnotationMode> { AnnotationMode.KvSchema },
                        TemplateConfigJson = "{\"name\":\"kv\"}",
                        IsBuiltIn = false
                    });
                }

                var engine = new ActiveLearningEngine(dbPath, modelsDir, new NerModel());
                engine.SubmitStructuredCorrection(
                    "设备A 子系统B 电压=12V",
                    scenarioId,
                    AnnotationMode.KvSchema,
                    "{\"rows\":[{\"设备\":\"设备A\",\"参数\":\"电压\",\"取值\":\"12V\"}]}");

                using var checkRepo = new ActiveLearningRepository(dbPath);
                var records = checkRepo.GetAnnotatedTexts(scenarioId);
                Assert.Single(records);
                Assert.Equal(AnnotationMode.KvSchema.ToString(), records[0].AnnotationMode);
                Assert.Contains("设备A", records[0].StructuredAnnotationsJson);
            }
            finally
            {
                SafeDeleteDir(root);
            }
        }

        private static string CreateTempDir()
        {
            string path = Path.Combine(Path.GetTempPath(), "docextractor-scenario-mode-tests-" + Guid.NewGuid().ToString("N"));
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
