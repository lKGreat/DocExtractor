using System;
using System.Collections.Generic;
using System.Data.SQLite;
using Newtonsoft.Json;

namespace DocExtractor.Data.Repositories
{
    /// <summary>场景定义：一组实体类型标签 + 名称描述</summary>
    public class NlpScenario
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        /// <summary>JSON 序列化的实体类型标签列表，如 ["Value","Unit","HexCode"]</summary>
        public List<string> EntityTypes { get; set; } = new List<string>();
        public string CreatedAt { get; set; } = string.Empty;
        public bool IsBuiltIn { get; set; }
    }

    /// <summary>一条标注好的文本样本（含实体标注 JSON）</summary>
    public class NlpAnnotatedText
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public string RawText { get; set; } = string.Empty;
        /// <summary>JSON 序列化的 List&lt;ActiveEntityAnnotation&gt;</summary>
        public string AnnotationsJson { get; set; } = "[]";
        public string Source { get; set; } = string.Empty;
        /// <summary>预测时的平均置信度（0~1）</summary>
        public float ConfidenceScore { get; set; }
        public bool IsVerified { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    /// <summary>实体标注条目（用于 AnnotationsJson）</summary>
    public class ActiveEntityAnnotation
    {
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public float Confidence { get; set; }
        public bool IsManual { get; set; }
    }

    /// <summary>一次增量训练会话的结果记录</summary>
    public class NlpLearningSession
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public int SampleCountBefore { get; set; }
        public int SampleCountAfter { get; set; }
        /// <summary>训练前质量指标 JSON</summary>
        public string MetricsBeforeJson { get; set; } = "{}";
        /// <summary>训练后质量指标 JSON</summary>
        public string MetricsAfterJson { get; set; } = "{}";
        public string TrainedAt { get; set; } = string.Empty;
        public double DurationSeconds { get; set; }
        public bool IsImproved { get; set; }
    }

    /// <summary>质量指标快照</summary>
    public class NlpQualityMetrics
    {
        /// <summary>统一主指标（兼容旧字段，等价于 MicroF1）</summary>
        public double F1 { get; set; }
        public double MicroF1 { get; set; }
        public double MacroF1 { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public int SampleCount { get; set; }
        /// <summary>每个实体类型的分项 F1：EntityType -> F1</summary>
        public Dictionary<string, double> PerTypeF1 { get; set; } = new Dictionary<string, double>();
        /// <summary>每个实体类型在 gold 标注中的支持度：EntityType -> Count</summary>
        public Dictionary<string, int> PerTypeSupport { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>不确定性队列条目：模型不确定、需要人工标注的文本</summary>
    public class NlpUncertainEntry
    {
        public int Id { get; set; }
        public int ScenarioId { get; set; }
        public string RawText { get; set; } = string.Empty;
        /// <summary>模型当前预测的 JSON（List&lt;ActiveEntityAnnotation&gt;）</summary>
        public string PredictionsJson { get; set; } = "[]";
        /// <summary>最低置信度（最不确定的 token 分数）</summary>
        public float MinConfidence { get; set; }
        public bool IsReviewed { get; set; }
        public bool IsSkipped { get; set; }
        public string SkipReason { get; set; } = string.Empty;
        public string ReviewedAt { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
    }

    /// <summary>
    /// 主动学习数据仓储：管理场景、标注文本、学习会话、不确定性队列
    /// </summary>
    public class ActiveLearningRepository : IDisposable
    {
        private readonly SQLiteConnection _conn;

        public ActiveLearningRepository(string dbPath)
        {
            _conn = new SQLiteConnection(
                $"Data Source={dbPath};Version=3;Pooling=True;Max Pool Size=100;Journal Mode=WAL;");
            _conn.Open();
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            const string sql = @"
CREATE TABLE IF NOT EXISTS NlpScenario (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    Name        TEXT    NOT NULL,
    Description TEXT    NOT NULL DEFAULT '',
    EntityTypesJson TEXT NOT NULL DEFAULT '[]',
    IsBuiltIn   INTEGER NOT NULL DEFAULT 0,
    CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS NlpAnnotatedText (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScenarioId      INTEGER NOT NULL,
    RawText         TEXT    NOT NULL,
    AnnotationsJson TEXT    NOT NULL DEFAULT '[]',
    Source          TEXT    NOT NULL DEFAULT '',
    ConfidenceScore REAL    NOT NULL DEFAULT 0,
    IsVerified      INTEGER NOT NULL DEFAULT 0,
    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS NlpLearningSession (
    Id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    ScenarioId          INTEGER NOT NULL,
    SampleCountBefore   INTEGER NOT NULL DEFAULT 0,
    SampleCountAfter    INTEGER NOT NULL DEFAULT 0,
    MetricsBeforeJson   TEXT    NOT NULL DEFAULT '{}',
    MetricsAfterJson    TEXT    NOT NULL DEFAULT '{}',
    TrainedAt           TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    DurationSeconds     REAL    NOT NULL DEFAULT 0,
    IsImproved          INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS NlpUncertainQueue (
    Id              INTEGER PRIMARY KEY AUTOINCREMENT,
    ScenarioId      INTEGER NOT NULL,
    RawText         TEXT    NOT NULL,
    PredictionsJson TEXT    NOT NULL DEFAULT '[]',
    MinConfidence   REAL    NOT NULL DEFAULT 1,
    IsReviewed      INTEGER NOT NULL DEFAULT 0,
    IsSkipped       INTEGER NOT NULL DEFAULT 0,
    SkipReason      TEXT    NOT NULL DEFAULT '',
    ReviewedAt      TEXT    NULL,
    CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);
";
            using var cmd = new SQLiteCommand(sql, _conn);
            cmd.ExecuteNonQuery();

            // 兼容已存在数据库：补齐新增列
            TryAddColumn("NlpUncertainQueue", "IsSkipped", "INTEGER NOT NULL DEFAULT 0");
            TryAddColumn("NlpUncertainQueue", "SkipReason", "TEXT NOT NULL DEFAULT ''");
            TryAddColumn("NlpUncertainQueue", "ReviewedAt", "TEXT NULL");
        }

        // ── 场景管理 ─────────────────────────────────────────────────────────

        public int AddScenario(NlpScenario scenario)
        {
            string entityTypesJson = JsonConvert.SerializeObject(scenario.EntityTypes);
            using var cmd = new SQLiteCommand(
                "INSERT INTO NlpScenario (Name, Description, EntityTypesJson, IsBuiltIn) VALUES (@n,@d,@e,@b); SELECT last_insert_rowid();",
                _conn);
            cmd.Parameters.AddWithValue("@n", scenario.Name);
            cmd.Parameters.AddWithValue("@d", scenario.Description);
            cmd.Parameters.AddWithValue("@e", entityTypesJson);
            cmd.Parameters.AddWithValue("@b", scenario.IsBuiltIn ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar()!;
        }

        public List<NlpScenario> GetAllScenarios()
        {
            using var cmd = new SQLiteCommand(
                "SELECT Id, Name, Description, EntityTypesJson, IsBuiltIn, CreatedAt FROM NlpScenario ORDER BY IsBuiltIn DESC, Id ASC",
                _conn);
            using var r = cmd.ExecuteReader();
            var result = new List<NlpScenario>();
            while (r.Read())
            {
                var s = new NlpScenario
                {
                    Id = r.GetInt32(0),
                    Name = r.GetString(1),
                    Description = r.GetString(2),
                    EntityTypes = JsonConvert.DeserializeObject<List<string>>(r.GetString(3)) ?? new List<string>(),
                    IsBuiltIn = r.GetInt32(4) == 1,
                    CreatedAt = r.GetString(5)
                };
                result.Add(s);
            }
            return result;
        }

        public NlpScenario? GetScenarioById(int id)
        {
            using var cmd = new SQLiteCommand(
                "SELECT Id, Name, Description, EntityTypesJson, IsBuiltIn, CreatedAt FROM NlpScenario WHERE Id=@id",
                _conn);
            cmd.Parameters.AddWithValue("@id", id);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new NlpScenario
            {
                Id = r.GetInt32(0),
                Name = r.GetString(1),
                Description = r.GetString(2),
                EntityTypes = JsonConvert.DeserializeObject<List<string>>(r.GetString(3)) ?? new List<string>(),
                IsBuiltIn = r.GetInt32(4) == 1,
                CreatedAt = r.GetString(5)
            };
        }

        public void DeleteScenario(int id)
        {
            using var cmd = new SQLiteCommand("DELETE FROM NlpScenario WHERE Id=@id AND IsBuiltIn=0", _conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── 标注文本 ─────────────────────────────────────────────────────────

        public int AddAnnotatedText(NlpAnnotatedText item)
        {
            using var cmd = new SQLiteCommand(
                "INSERT INTO NlpAnnotatedText (ScenarioId, RawText, AnnotationsJson, Source, ConfidenceScore, IsVerified) " +
                "VALUES (@s,@t,@a,@src,@c,@v); SELECT last_insert_rowid();",
                _conn);
            cmd.Parameters.AddWithValue("@s", item.ScenarioId);
            cmd.Parameters.AddWithValue("@t", item.RawText);
            cmd.Parameters.AddWithValue("@a", item.AnnotationsJson);
            cmd.Parameters.AddWithValue("@src", item.Source);
            cmd.Parameters.AddWithValue("@c", item.ConfidenceScore);
            cmd.Parameters.AddWithValue("@v", item.IsVerified ? 1 : 0);
            return (int)(long)cmd.ExecuteScalar()!;
        }

        public void UpdateAnnotation(int id, string annotationsJson, bool isVerified)
        {
            using var cmd = new SQLiteCommand(
                "UPDATE NlpAnnotatedText SET AnnotationsJson=@a, IsVerified=@v WHERE Id=@id",
                _conn);
            cmd.Parameters.AddWithValue("@a", annotationsJson);
            cmd.Parameters.AddWithValue("@v", isVerified ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public List<NlpAnnotatedText> GetAnnotatedTexts(int scenarioId, bool verifiedOnly = false)
        {
            string sql = verifiedOnly
                ? "SELECT Id, ScenarioId, RawText, AnnotationsJson, Source, ConfidenceScore, IsVerified, CreatedAt FROM NlpAnnotatedText WHERE ScenarioId=@s AND IsVerified=1 ORDER BY Id DESC"
                : "SELECT Id, ScenarioId, RawText, AnnotationsJson, Source, ConfidenceScore, IsVerified, CreatedAt FROM NlpAnnotatedText WHERE ScenarioId=@s ORDER BY Id DESC";
            using var cmd = new SQLiteCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@s", scenarioId);
            using var r = cmd.ExecuteReader();
            var result = new List<NlpAnnotatedText>();
            while (r.Read())
            {
                result.Add(new NlpAnnotatedText
                {
                    Id = r.GetInt32(0),
                    ScenarioId = r.GetInt32(1),
                    RawText = r.GetString(2),
                    AnnotationsJson = r.GetString(3),
                    Source = r.IsDBNull(4) ? "" : r.GetString(4),
                    ConfidenceScore = r.IsDBNull(5) ? 0f : (float)r.GetDouble(5),
                    IsVerified = r.GetInt32(6) == 1,
                    CreatedAt = r.GetString(7)
                });
            }
            return result;
        }

        public int GetAnnotatedTextCount(int scenarioId) =>
            (int)(long)ExecuteScalar("SELECT COUNT(*) FROM NlpAnnotatedText WHERE ScenarioId=@s", scenarioId)!;

        public int GetVerifiedCount(int scenarioId) =>
            (int)(long)ExecuteScalar("SELECT COUNT(*) FROM NlpAnnotatedText WHERE ScenarioId=@s AND IsVerified=1", scenarioId)!;

        public void DeleteAnnotatedText(int id)
        {
            using var cmd = new SQLiteCommand("DELETE FROM NlpAnnotatedText WHERE Id=@id", _conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        // ── 学习会话 ─────────────────────────────────────────────────────────

        public void SaveLearningSession(NlpLearningSession session)
        {
            using var cmd = new SQLiteCommand(
                "INSERT INTO NlpLearningSession (ScenarioId, SampleCountBefore, SampleCountAfter, MetricsBeforeJson, MetricsAfterJson, DurationSeconds, IsImproved) " +
                "VALUES (@sid,@sb,@sa,@mb,@ma,@d,@imp)",
                _conn);
            cmd.Parameters.AddWithValue("@sid", session.ScenarioId);
            cmd.Parameters.AddWithValue("@sb", session.SampleCountBefore);
            cmd.Parameters.AddWithValue("@sa", session.SampleCountAfter);
            cmd.Parameters.AddWithValue("@mb", session.MetricsBeforeJson);
            cmd.Parameters.AddWithValue("@ma", session.MetricsAfterJson);
            cmd.Parameters.AddWithValue("@d", session.DurationSeconds);
            cmd.Parameters.AddWithValue("@imp", session.IsImproved ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public List<NlpLearningSession> GetLearningSessions(int scenarioId)
        {
            using var cmd = new SQLiteCommand(
                "SELECT Id, ScenarioId, SampleCountBefore, SampleCountAfter, MetricsBeforeJson, MetricsAfterJson, TrainedAt, DurationSeconds, IsImproved " +
                "FROM NlpLearningSession WHERE ScenarioId=@s ORDER BY Id ASC",
                _conn);
            cmd.Parameters.AddWithValue("@s", scenarioId);
            using var r = cmd.ExecuteReader();
            var result = new List<NlpLearningSession>();
            while (r.Read())
            {
                result.Add(new NlpLearningSession
                {
                    Id = r.GetInt32(0),
                    ScenarioId = r.GetInt32(1),
                    SampleCountBefore = r.GetInt32(2),
                    SampleCountAfter = r.GetInt32(3),
                    MetricsBeforeJson = r.GetString(4),
                    MetricsAfterJson = r.GetString(5),
                    TrainedAt = r.GetString(6),
                    DurationSeconds = r.GetDouble(7),
                    IsImproved = r.GetInt32(8) == 1
                });
            }
            return result;
        }

        // ── 不确定性队列 ──────────────────────────────────────────────────────

        public void AddUncertainEntry(NlpUncertainEntry entry)
        {
            using var cmd = new SQLiteCommand(
                "INSERT INTO NlpUncertainQueue (ScenarioId, RawText, PredictionsJson, MinConfidence) " +
                "SELECT @s,@t,@p,@c WHERE NOT EXISTS (" +
                "SELECT 1 FROM NlpUncertainQueue WHERE ScenarioId=@s AND RawText=@t)",
                _conn);
            cmd.Parameters.AddWithValue("@s", entry.ScenarioId);
            cmd.Parameters.AddWithValue("@t", entry.RawText);
            cmd.Parameters.AddWithValue("@p", entry.PredictionsJson);
            cmd.Parameters.AddWithValue("@c", entry.MinConfidence);
            cmd.ExecuteNonQuery();
        }

        public List<NlpUncertainEntry> GetUncertainQueue(int scenarioId, int topN = 50)
        {
            using var cmd = new SQLiteCommand(
                "SELECT Id, ScenarioId, RawText, PredictionsJson, MinConfidence, IsReviewed, IsSkipped, SkipReason, ReviewedAt, CreatedAt " +
                "FROM NlpUncertainQueue WHERE ScenarioId=@s AND IsReviewed=0 ORDER BY MinConfidence ASC LIMIT @n",
                _conn);
            cmd.Parameters.AddWithValue("@s", scenarioId);
            cmd.Parameters.AddWithValue("@n", topN);
            using var r = cmd.ExecuteReader();
            var result = new List<NlpUncertainEntry>();
            while (r.Read())
            {
                result.Add(new NlpUncertainEntry
                {
                    Id = r.GetInt32(0),
                    ScenarioId = r.GetInt32(1),
                    RawText = r.GetString(2),
                    PredictionsJson = r.GetString(3),
                    MinConfidence = (float)r.GetDouble(4),
                    IsReviewed = r.GetInt32(5) == 1,
                    IsSkipped = r.GetInt32(6) == 1,
                    SkipReason = r.IsDBNull(7) ? "" : r.GetString(7),
                    ReviewedAt = r.IsDBNull(8) ? "" : r.GetString(8),
                    CreatedAt = r.GetString(9)
                });
            }
            return result;
        }

        public void MarkUncertainReviewed(int id, bool isSkipped = false, string skipReason = "")
        {
            using var cmd = new SQLiteCommand(
                "UPDATE NlpUncertainQueue SET IsReviewed=1, IsSkipped=@skip, SkipReason=@reason, ReviewedAt=datetime('now','localtime') WHERE Id=@id",
                _conn);
            cmd.Parameters.AddWithValue("@skip", isSkipped ? 1 : 0);
            cmd.Parameters.AddWithValue("@reason", skipReason ?? string.Empty);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        public int GetPendingUncertainCount(int scenarioId) =>
            (int)(long)ExecuteScalar("SELECT COUNT(*) FROM NlpUncertainQueue WHERE ScenarioId=@s AND IsReviewed=0", scenarioId)!;

        public void ClearUncertainQueue(int scenarioId)
        {
            using var cmd = new SQLiteCommand("DELETE FROM NlpUncertainQueue WHERE ScenarioId=@s", _conn);
            cmd.Parameters.AddWithValue("@s", scenarioId);
            cmd.ExecuteNonQuery();
        }

        // ── 辅助 ─────────────────────────────────────────────────────────────

        private object? ExecuteScalar(string sql, int scenarioId)
        {
            using var cmd = new SQLiteCommand(sql, _conn);
            cmd.Parameters.AddWithValue("@s", scenarioId);
            return cmd.ExecuteScalar();
        }

        private void TryAddColumn(string tableName, string columnName, string sqlType)
        {
            try
            {
                using var cmd = new SQLiteCommand($"ALTER TABLE {tableName} ADD COLUMN {columnName} {sqlType}", _conn);
                cmd.ExecuteNonQuery();
            }
            catch
            {
                // 已存在则忽略
            }
        }

        public void Dispose() => _conn?.Dispose();
    }
}
