using System;
using System.IO;
using DocExtractor.Core.Models;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.ColumnClassifier;
using DocExtractor.ML.EntityExtractor;
using DocExtractor.ML.SectionClassifier;
using DocExtractor.UI.Logging;
using DocExtractor.UI.Services;
using Microsoft.Extensions.Logging;

namespace DocExtractor.UI.Context
{
    /// <summary>
    /// Shared application state container. Injected into every UserControl via constructor.
    /// Owns ML models, services, current config, and cross-panel events.
    /// </summary>
    internal sealed class DocExtractorContext : IDisposable
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        public string DbPath { get; }
        public string ModelsDir { get; }
        public string StartupPath { get; }

        // ── ML Models ─────────────────────────────────────────────────────────
        public ColumnClassifierModel ColumnModel { get; } = new ColumnClassifierModel();
        public NerModel NerModel { get; } = new NerModel();
        public SectionClassifierModel SectionModel { get; } = new SectionClassifierModel();

        // ── Config State ──────────────────────────────────────────────────────
        public ExtractionConfig CurrentConfig { get; set; } = new ExtractionConfig();
        public int CurrentConfigId { get; set; } = -1;

        // ── Repositories & Services ───────────────────────────────────────────
        public ExtractionConfigRepository ConfigRepo { get; }
        public ConfigWorkflowService ConfigService { get; }
        public ExtractionWorkflowService ExtractionService { get; } = new ExtractionWorkflowService();
        public TrainingWorkflowService TrainingService { get; } = new TrainingWorkflowService();
        public RecommendationService RecommendationService { get; } = new RecommendationService();

        // ── Logging ───────────────────────────────────────────────────────────
        public ILoggerFactory LoggerFactory { get; private set; }
        public ILogger Logger { get; private set; }
        public ILogger TrainLogger { get; private set; }

        // ── Events ────────────────────────────────────────────────────────────
        /// <summary>Fired when the active config changes or is saved. Panels refresh their grids.</summary>
        public event Action ConfigChanged;

        /// <summary>Fired when the config list changes (new/delete). MainForm reloads the combo.</summary>
        public event Action ConfigListChanged;

        /// <summary>Fired when models are (re)loaded. Panels may update health indicators.</summary>
        public event Action ModelsReloaded;

        /// <summary>Fired to update the main status bar text.</summary>
        public event Action<string> StatusMessage;

        /// <summary>Pipeline log lines routed to ExtractionPanel log box.</summary>
        public event Action<string> LogLine;

        /// <summary>Training log lines routed to TrainingPanel log box.</summary>
        public event Action<string> TrainLogLine;

        public DocExtractorContext(string startupPath)
        {
            StartupPath = startupPath;
            DbPath = Path.Combine(startupPath, "data", "docextractor.db");
            ModelsDir = Path.Combine(startupPath, "models");

            Directory.CreateDirectory(Path.GetDirectoryName(DbPath));
            Directory.CreateDirectory(ModelsDir);

            ConfigRepo = new ExtractionConfigRepository(DbPath);
            ConfigService = new ConfigWorkflowService(ConfigRepo);
        }

        public void InitializeLogging()
        {
            string logDir = Path.Combine(StartupPath, "logs");
            LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(new UiFileLoggerProvider(RouteLogToUi, logDir));
            });
            Logger = LoggerFactory.CreateLogger("Pipeline");
            TrainLogger = LoggerFactory.CreateLogger("Training");
            Logger.LogInformation("应用启动：DocExtractor UI 初始化完成");
        }

        private void RouteLogToUi(string category, string line)
        {
            if (string.Equals(category, "Training", StringComparison.OrdinalIgnoreCase))
                TrainLogLine?.Invoke(line);
            else
                LogLine?.Invoke(line);
        }

        public void TryLoadModels()
        {
            try { if (File.Exists(Path.Combine(ModelsDir, "column_classifier.zip"))) ColumnModel.Load(Path.Combine(ModelsDir, "column_classifier.zip")); } catch { }
            try { if (File.Exists(Path.Combine(ModelsDir, "ner_model.zip"))) NerModel.Load(Path.Combine(ModelsDir, "ner_model.zip")); } catch { }
            try { if (File.Exists(Path.Combine(ModelsDir, "section_classifier.zip"))) SectionModel.Load(Path.Combine(ModelsDir, "section_classifier.zip")); } catch { }
        }

        public void ReloadModelByName(string modelName)
        {
            try
            {
                if (string.Equals(modelName, "column_classifier", StringComparison.OrdinalIgnoreCase))
                {
                    string p = Path.Combine(ModelsDir, "column_classifier.zip");
                    if (File.Exists(p)) ColumnModel.Reload(p);
                }
                else if (string.Equals(modelName, "ner_model", StringComparison.OrdinalIgnoreCase))
                {
                    string p = Path.Combine(ModelsDir, "ner_model.zip");
                    if (File.Exists(p)) NerModel.Load(p);
                }
                else if (string.Equals(modelName, "section_classifier", StringComparison.OrdinalIgnoreCase))
                {
                    string p = Path.Combine(ModelsDir, "section_classifier.zip");
                    if (File.Exists(p)) SectionModel.Load(p);
                }
                Logger?.LogInformation($"模型已重载：{modelName}");
                ModelsReloaded?.Invoke();
            }
            catch (Exception ex)
            {
                Logger?.LogWarning($"模型重载失败：{modelName} - {ex.Message}");
            }
        }

        public void NotifyConfigChanged() => ConfigChanged?.Invoke();
        public void NotifyConfigListChanged() => ConfigListChanged?.Invoke();
        public void NotifyStatus(string message) => StatusMessage?.Invoke(message);

        public void Dispose() => LoggerFactory?.Dispose();
    }
}
