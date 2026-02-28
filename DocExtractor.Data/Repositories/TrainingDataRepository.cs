using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using DocExtractor.ML.EntityExtractor;

namespace DocExtractor.Data.Repositories
{
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
);";
    }
}
