using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using DocExtractor.ML.EntityExtractor;

namespace DocExtractor.Data.Repositories
{
    /// <summary>训练历史记录</summary>
    public class TrainingRecord
    {
        public int SampleCount { get; set; }
        public string MetricsJson { get; set; }
        public string ParametersJson { get; set; }
        public string TrainedAt { get; set; }
    }

    /// <summary>章节标题标注数据</summary>
    public class SectionAnnotation
    {
        public string ParagraphText { get; set; } = string.Empty;
        public bool IsHeading { get; set; }
        public bool IsBold { get; set; }
        public float FontSize { get; set; }
        public bool HasHeadingStyle { get; set; }
        public int OutlineLevel { get; set; } = 9;
    }

    /// <summary>
    /// 训练数据仓储：管理列名和NER的训练样本持久化
    /// </summary>
    public class TrainingDataRepository : IDisposable
    {
        private readonly SQLiteConnection _conn;

        public TrainingDataRepository(string dbPath)
        {
            _conn = new SQLiteConnection(
                $"Data Source={dbPath};Version=3;Pooling=True;Max Pool Size=100;Journal Mode=WAL;");
            _conn.Open();
            // 始终执行建表（全部用 CREATE TABLE IF NOT EXISTS，幂等安全）
            // 数据库文件由 ExtractionConfigRepository 先创建，此处补齐训练相关表
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            string schemaPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Schema", "schema.sql");

            string sql = File.Exists(schemaPath)
                ? File.ReadAllText(schemaPath)
                : GetEmbeddedSchema();

            using var cmd = new SQLiteCommand(sql, _conn);
            cmd.ExecuteNonQuery();
        }

        // ── 列名训练数据 ─────────────────────────────────────────────────────

        public void AddColumnSample(
            string columnText,
            string fieldName,
            string? source = null,
            bool isVerified = false)
        {
            using var cmd = new SQLiteCommand(
                "INSERT INTO ColumnTrainingData (ColumnText, FieldName, Source, IsVerified) VALUES (@t, @f, @s, @v)",
                _conn);
            cmd.Parameters.AddWithValue("@t", columnText);
            cmd.Parameters.AddWithValue("@f", fieldName);
            cmd.Parameters.AddWithValue("@s", source ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@v", isVerified ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public List<(string ColumnText, string FieldName)> GetColumnSamples(bool verifiedOnly = false)
        {
            string sql = verifiedOnly
                ? "SELECT ColumnText, FieldName FROM ColumnTrainingData WHERE IsVerified=1"
                : "SELECT ColumnText, FieldName FROM ColumnTrainingData";

            using var cmd = new SQLiteCommand(sql, _conn);
            using var reader = cmd.ExecuteReader();

            var result = new List<(string, string)>();
            while (reader.Read())
                result.Add((reader.GetString(0), reader.GetString(1)));
            return result;
        }

        public int GetColumnSampleCount() =>
            (int)(long)new SQLiteCommand(
                "SELECT COUNT(*) FROM ColumnTrainingData", _conn).ExecuteScalar()!;

        public int GetVerifiedColumnSampleCount(string? source = null)
        {
            string sql = "SELECT COUNT(*) FROM ColumnTrainingData WHERE IsVerified=1";
            if (!string.IsNullOrWhiteSpace(source))
                sql += " AND Source=@s";

            using var cmd = new SQLiteCommand(sql, _conn);
            if (!string.IsNullOrWhiteSpace(source))
                cmd.Parameters.AddWithValue("@s", source);

            return (int)(long)cmd.ExecuteScalar()!;
        }

        // ── NER 训练数据 ──────────────────────────────────────────────────────

        public void AddNerSample(string cellText, List<EntityAnnotation> entities, string? source = null)
        {
            string json = JsonConvert.SerializeObject(entities);
            using var cmd = new SQLiteCommand(
                "INSERT INTO NerTrainingData (CellText, AnnotationJson, Source) VALUES (@t, @j, @s)",
                _conn);
            cmd.Parameters.AddWithValue("@t", cellText);
            cmd.Parameters.AddWithValue("@j", json);
            cmd.Parameters.AddWithValue("@s", source ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public List<NerAnnotation> GetNerSamples()
        {
            using var cmd = new SQLiteCommand(
                "SELECT CellText, AnnotationJson FROM NerTrainingData", _conn);
            using var reader = cmd.ExecuteReader();

            var result = new List<NerAnnotation>();
            while (reader.Read())
            {
                var entities = JsonConvert.DeserializeObject<List<EntityAnnotation>>(reader.GetString(1))
                               ?? new List<EntityAnnotation>();
                result.Add(new NerAnnotation { Text = reader.GetString(0), Entities = entities });
            }
            return result;
        }

        public int GetNerSampleCount() =>
            (int)(long)new SQLiteCommand(
                "SELECT COUNT(*) FROM NerTrainingData", _conn).ExecuteScalar()!;

        // ── 章节标题训练数据 ──────────────────────────────────────────────────

        /// <summary>
        /// 添加章节标题标注样本
        /// </summary>
        /// <param name="paragraphText">段落文本</param>
        /// <param name="isHeading">是否为章节标题</param>
        /// <param name="isBold">是否加粗</param>
        /// <param name="fontSize">字号（半磅）</param>
        /// <param name="hasHeadingStyle">是否有标题样式</param>
        /// <param name="outlineLevel">大纲级别</param>
        /// <param name="source">来源文件</param>
        public void AddSectionSample(
            string paragraphText,
            bool isHeading,
            bool isBold,
            float fontSize,
            bool hasHeadingStyle,
            int outlineLevel,
            string? source = null)
        {
            EnsureSectionTable();
            using var cmd = new SQLiteCommand(
                "INSERT INTO SectionTrainingData (ParagraphText, IsHeading, IsBold, FontSize, HasHeadingStyle, OutlineLevel, Source) " +
                "VALUES (@t, @h, @b, @fs, @hs, @ol, @s)", _conn);
            cmd.Parameters.AddWithValue("@t", paragraphText);
            cmd.Parameters.AddWithValue("@h", isHeading ? 1 : 0);
            cmd.Parameters.AddWithValue("@b", isBold ? 1 : 0);
            cmd.Parameters.AddWithValue("@fs", fontSize);
            cmd.Parameters.AddWithValue("@hs", hasHeadingStyle ? 1 : 0);
            cmd.Parameters.AddWithValue("@ol", outlineLevel);
            cmd.Parameters.AddWithValue("@s", source ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public List<SectionAnnotation> GetSectionSamples()
        {
            EnsureSectionTable();
            using var cmd = new SQLiteCommand(
                "SELECT ParagraphText, IsHeading, IsBold, FontSize, HasHeadingStyle, OutlineLevel FROM SectionTrainingData",
                _conn);
            using var reader = cmd.ExecuteReader();

            var result = new List<SectionAnnotation>();
            while (reader.Read())
            {
                result.Add(new SectionAnnotation
                {
                    ParagraphText = reader.GetString(0),
                    IsHeading = reader.GetInt32(1) == 1,
                    IsBold = reader.GetInt32(2) == 1,
                    FontSize = reader.IsDBNull(3) ? 0f : (float)reader.GetDouble(3),
                    HasHeadingStyle = reader.GetInt32(4) == 1,
                    OutlineLevel = reader.IsDBNull(5) ? 9 : reader.GetInt32(5)
                });
            }
            return result;
        }

        public int GetSectionSampleCount()
        {
            EnsureSectionTable();
            return (int)(long)new SQLiteCommand(
                "SELECT COUNT(*) FROM SectionTrainingData", _conn).ExecuteScalar()!;
        }

        private void EnsureSectionTable()
        {
            using var cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS SectionTrainingData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ParagraphText TEXT NOT NULL,
    IsHeading INTEGER NOT NULL DEFAULT 0,
    IsBold INTEGER NOT NULL DEFAULT 0,
    FontSize REAL NOT NULL DEFAULT 0,
    HasHeadingStyle INTEGER NOT NULL DEFAULT 0,
    OutlineLevel INTEGER NOT NULL DEFAULT 9,
    Source TEXT,
    CreatedAt TEXT DEFAULT (datetime('now'))
);", _conn);
            cmd.ExecuteNonQuery();
        }

        // ── 模型训练历史 ─────────────────────────────────────────────────

        /// <summary>保存一次训练记录（用于 before/after 对比）</summary>
        public void SaveTrainingRecord(string modelType, int sampleCount, string metricsJson, string parametersJson)
        {
            EnsureHistoryTable();
            using var cmd = new SQLiteCommand(
                "INSERT INTO ModelTrainingHistory (ModelType, SampleCount, MetricsJson, ParametersJson) " +
                "VALUES (@mt, @sc, @mj, @pj)", _conn);
            cmd.Parameters.AddWithValue("@mt", modelType);
            cmd.Parameters.AddWithValue("@sc", sampleCount);
            cmd.Parameters.AddWithValue("@mj", metricsJson);
            cmd.Parameters.AddWithValue("@pj", parametersJson ?? (object)DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        /// <summary>获取某模型最近一次训练记录</summary>
        public TrainingRecord GetLatestRecord(string modelType)
        {
            EnsureHistoryTable();
            using var cmd = new SQLiteCommand(
                "SELECT SampleCount, MetricsJson, ParametersJson, TrainedAt " +
                "FROM ModelTrainingHistory WHERE ModelType=@mt ORDER BY Id DESC LIMIT 1", _conn);
            cmd.Parameters.AddWithValue("@mt", modelType);

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return null;

            return new TrainingRecord
            {
                SampleCount = reader.GetInt32(0),
                MetricsJson = reader.GetString(1),
                ParametersJson = reader.IsDBNull(2) ? null : reader.GetString(2),
                TrainedAt = reader.GetString(3)
            };
        }

        private void EnsureHistoryTable()
        {
            using var cmd = new SQLiteCommand(@"
CREATE TABLE IF NOT EXISTS ModelTrainingHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ModelType TEXT NOT NULL,
    TrainedAt TEXT DEFAULT (datetime('now')),
    SampleCount INTEGER,
    MetricsJson TEXT,
    ParametersJson TEXT
);", _conn);
            cmd.ExecuteNonQuery();
        }

        // ── 从知识库自动生成训练数据 ─────────────────────────────────────────

        /// <summary>
        /// 从 GroupItemKnowledge 和 BuiltInConfigs 自动生成训练样本，无需手工导入。
        ///
        /// 生成规则：
        ///   列名分类器：BuiltInConfigs 所有配置的 KnownColumnVariants → ColumnTrainingData
        ///   章节标题分类器：
        ///     正样本 — GroupItemKnowledge 中的唯一组名 → IsHeading=true
        ///     负样本 — GroupItemKnowledge 中的唯一细则项名 → IsHeading=false
        ///
        /// 已存在的完全相同文本不重复插入（基于 ColumnText/ParagraphText 唯一约束跳过）。
        /// </summary>
        /// <returns>(columnSamplesAdded, sectionPositiveAdded, sectionNegativeAdded)</returns>
        public (int colAdded, int secPosAdded, int secNegAdded) GenerateFromKnowledge(
            DocExtractor.Core.Models.ExtractionConfig[] configs)
        {
            int colAdded = 0, secPosAdded = 0, secNegAdded = 0;

            // ── 1. 列名分类器：从 BuiltInConfigs.KnownColumnVariants 生成 ──────
            // 查询已有文本（避免重复）
            var existingColTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SQLiteCommand("SELECT ColumnText FROM ColumnTrainingData", _conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    existingColTexts.Add(reader.GetString(0));

            using (var tx = _conn.BeginTransaction())
            {
                using var insertCol = new SQLiteCommand(
                    "INSERT INTO ColumnTrainingData (ColumnText, FieldName, Source) VALUES (@t, @f, 'KnowledgeBase')",
                    _conn, tx);
                var pColText = insertCol.Parameters.Add("@t", System.Data.DbType.String);
                var pColField = insertCol.Parameters.Add("@f", System.Data.DbType.String);

                foreach (var config in configs)
                {
                    foreach (var field in config.Fields)
                    {
                        // DisplayName 本身也是合法的列名变体
                        var variants = new List<string>(field.KnownColumnVariants);
                        if (!string.IsNullOrWhiteSpace(field.DisplayName))
                            variants.Add(field.DisplayName);

                        foreach (var variant in variants)
                        {
                            if (string.IsNullOrWhiteSpace(variant)) continue;
                            string v = variant.Trim();
                            if (existingColTexts.Contains(v)) continue;

                            pColText.Value = v;
                            pColField.Value = field.FieldName;
                            insertCol.ExecuteNonQuery();
                            existingColTexts.Add(v);
                            colAdded++;
                        }
                    }
                }
                tx.Commit();
            }

            // ── 2. 章节标题分类器：从 GroupItemKnowledge 生成正/负样本 ──────────
            // 读取已有段落文本（避免重复）
            EnsureSectionTable();
            var existingSecTexts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var cmd = new SQLiteCommand("SELECT ParagraphText FROM SectionTrainingData", _conn))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    existingSecTexts.Add(reader.GetString(0));

            // 读取知识库中的唯一组名（正样本）和唯一细则项名（负样本）
            var groupNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var itemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            bool hasKnowledgeTable = TableExists("GroupItemKnowledge");
            if (hasKnowledgeTable)
            {
                using (var cmd = new SQLiteCommand(
                    "SELECT DISTINCT GroupNameNormalized FROM GroupItemKnowledge WHERE GroupNameNormalized != ''",
                    _conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) groupNames.Add(reader.GetString(0));

                using (var cmd = new SQLiteCommand(
                    "SELECT DISTINCT ItemName FROM GroupItemKnowledge WHERE ItemName != ''",
                    _conn))
                using (var reader = cmd.ExecuteReader())
                    while (reader.Read()) itemNames.Add(reader.GetString(0));
            }

            using (var tx = _conn.BeginTransaction())
            {
                using var insertSec = new SQLiteCommand(@"
INSERT INTO SectionTrainingData (ParagraphText, IsHeading, IsBold, FontSize, HasHeadingStyle, OutlineLevel, Source)
VALUES (@t, @h, @b, 0, 0, 9, 'KnowledgeBase')", _conn, tx);
                var pSecText = insertSec.Parameters.Add("@t", System.Data.DbType.String);
                var pSecHeading = insertSec.Parameters.Add("@h", System.Data.DbType.Int32);
                var pSecBold = insertSec.Parameters.Add("@b", System.Data.DbType.Int32);

                // 正样本：知识库中的组名
                foreach (var gn in groupNames)
                {
                    if (string.IsNullOrWhiteSpace(gn) || gn.Length > 80) continue;
                    if (existingSecTexts.Contains(gn)) continue;

                    pSecText.Value = gn;
                    pSecHeading.Value = 1;
                    // 组名通常不加粗（在知识库里已归一化，无格式信息）
                    pSecBold.Value = 0;
                    insertSec.ExecuteNonQuery();
                    existingSecTexts.Add(gn);
                    secPosAdded++;
                }

                // 负样本：知识库中的细则项名（排除与组名重复的）
                foreach (var item in itemNames)
                {
                    if (string.IsNullOrWhiteSpace(item) || item.Length > 80) continue;
                    if (existingSecTexts.Contains(item)) continue;
                    if (groupNames.Contains(item)) continue;  // 避免互相矛盾

                    pSecText.Value = item;
                    pSecHeading.Value = 0;
                    pSecBold.Value = 0;
                    insertSec.ExecuteNonQuery();
                    existingSecTexts.Add(item);
                    secNegAdded++;
                }

                tx.Commit();
            }

            return (colAdded, secPosAdded, secNegAdded);
        }

        private bool TableExists(string tableName)
        {
            using var cmd = new SQLiteCommand(
                $"SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='{tableName}'",
                _conn);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        public void Dispose() => _conn.Dispose();

        private static string GetEmbeddedSchema() => @"
CREATE TABLE IF NOT EXISTS ColumnTrainingData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ColumnText TEXT NOT NULL,
    FieldName TEXT NOT NULL,
    Source TEXT,
    CreatedAt TEXT DEFAULT (datetime('now')),
    IsVerified INTEGER DEFAULT 0
);
CREATE TABLE IF NOT EXISTS NerTrainingData (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    CellText TEXT NOT NULL,
    AnnotationJson TEXT NOT NULL,
    Source TEXT,
    CreatedAt TEXT DEFAULT (datetime('now')),
    IsVerified INTEGER DEFAULT 0
);
CREATE TABLE IF NOT EXISTS ExtractionConfig (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigName TEXT NOT NULL UNIQUE,
    ConfigJson TEXT NOT NULL,
    CreatedAt TEXT DEFAULT (datetime('now')),
    UpdatedAt TEXT DEFAULT (datetime('now'))
);
CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT
);";
    }
}
