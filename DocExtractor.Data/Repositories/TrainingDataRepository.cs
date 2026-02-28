using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using DocExtractor.ML.EntityExtractor;

namespace DocExtractor.Data.Repositories
{
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
            bool isNew = !File.Exists(dbPath);
            _conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            _conn.Open();

            if (isNew)
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

        public void AddColumnSample(string columnText, string fieldName, string? source = null)
        {
            using var cmd = new SQLiteCommand(
                "INSERT INTO ColumnTrainingData (ColumnText, FieldName, Source) VALUES (@t, @f, @s)",
                _conn);
            cmd.Parameters.AddWithValue("@t", columnText);
            cmd.Parameters.AddWithValue("@f", fieldName);
            cmd.Parameters.AddWithValue("@s", source ?? (object)DBNull.Value);
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
