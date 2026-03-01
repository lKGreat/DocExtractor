using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using DocExtractor.Core.Models;
using Newtonsoft.Json;

namespace DocExtractor.Data.Repositories
{
    /// <summary>
    /// 抽取配置仓储：管理 ExtractionConfig 的 CRUD 和默认配置持久化
    /// </summary>
    public class ExtractionConfigRepository : IDisposable
    {
        private readonly SQLiteConnection _conn;

        public ExtractionConfigRepository(string dbPath)
        {
            bool isNew = !File.Exists(dbPath);
            _conn = new SQLiteConnection(
                $"Data Source={dbPath};Version=3;Pooling=True;Max Pool Size=100;Journal Mode=WAL;");
            _conn.Open();

            if (isNew)
                InitializeSchema();
            else
                EnsureTables();
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

        /// <summary>确保必需的表存在（数据库已存在但可能缺少新表）</summary>
        private void EnsureTables()
        {
            using var cmd = new SQLiteCommand(GetEmbeddedSchema(), _conn);
            cmd.ExecuteNonQuery();
        }

        // ── 配置 CRUD ──────────────────────────────────────────────────────

        /// <summary>获取所有配置（仅 Id + Name，用于填充下拉列表）</summary>
        public List<(int Id, string Name)> GetAll()
        {
            using var cmd = new SQLiteCommand(
                "SELECT Id, ConfigName FROM ExtractionConfig ORDER BY Id", _conn);
            using var reader = cmd.ExecuteReader();

            var result = new List<(int, string)>();
            while (reader.Read())
                result.Add((reader.GetInt32(0), reader.GetString(1)));
            return result;
        }

        /// <summary>根据 Id 加载完整配置</summary>
        public ExtractionConfig? GetById(int id)
        {
            using var cmd = new SQLiteCommand(
                "SELECT ConfigJson FROM ExtractionConfig WHERE Id=@id", _conn);
            cmd.Parameters.AddWithValue("@id", id);

            var json = cmd.ExecuteScalar() as string;
            if (json == null) return null;

            var config = JsonConvert.DeserializeObject<ExtractionConfig>(json);
            return config;
        }

        /// <summary>
        /// 保存配置（INSERT 或 UPDATE）。返回配置的 Id。
        /// </summary>
        public int Save(ExtractionConfig config)
        {
            string json = JsonConvert.SerializeObject(config, Formatting.None);

            // 检查是否已存在同名配置
            using (var checkCmd = new SQLiteCommand(
                "SELECT Id FROM ExtractionConfig WHERE ConfigName=@name", _conn))
            {
                checkCmd.Parameters.AddWithValue("@name", config.ConfigName);
                var existingId = checkCmd.ExecuteScalar();

                if (existingId != null)
                {
                    // UPDATE
                    int id = Convert.ToInt32(existingId);
                    using var updateCmd = new SQLiteCommand(
                        "UPDATE ExtractionConfig SET ConfigJson=@json, UpdatedAt=datetime('now') WHERE Id=@id",
                        _conn);
                    updateCmd.Parameters.AddWithValue("@json", json);
                    updateCmd.Parameters.AddWithValue("@id", id);
                    updateCmd.ExecuteNonQuery();
                    return id;
                }
            }

            // INSERT
            using var insertCmd = new SQLiteCommand(
                "INSERT INTO ExtractionConfig (ConfigName, ConfigJson) VALUES (@name, @json)",
                _conn);
            insertCmd.Parameters.AddWithValue("@name", config.ConfigName);
            insertCmd.Parameters.AddWithValue("@json", json);
            insertCmd.ExecuteNonQuery();

            return (int)_conn.LastInsertRowId;
        }

        /// <summary>删除配置。内置配置不可删除。</summary>
        public bool Delete(int id)
        {
            // 检查是否为内置配置
            using (var checkCmd = new SQLiteCommand(
                "SELECT ConfigName FROM ExtractionConfig WHERE Id=@id", _conn))
            {
                checkCmd.Parameters.AddWithValue("@id", id);
                var name = checkCmd.ExecuteScalar() as string;
                if (name != null && BuiltInConfigs.BuiltInNames.Contains(name))
                    return false;
            }

            using var cmd = new SQLiteCommand(
                "DELETE FROM ExtractionConfig WHERE Id=@id", _conn);
            cmd.Parameters.AddWithValue("@id", id);
            return cmd.ExecuteNonQuery() > 0;
        }

        // ── 默认配置 ────────────────────────────────────────────────────────

        /// <summary>获取默认配置 Id（未设置返回 -1）</summary>
        public int GetDefaultConfigId()
        {
            using var cmd = new SQLiteCommand(
                "SELECT Value FROM AppSettings WHERE Key='DefaultConfigId'", _conn);
            var val = cmd.ExecuteScalar() as string;
            return val != null && int.TryParse(val, out int id) ? id : -1;
        }

        /// <summary>设置默认配置 Id</summary>
        public void SetDefaultConfigId(int id)
        {
            using var cmd = new SQLiteCommand(
                "INSERT OR REPLACE INTO AppSettings (Key, Value) VALUES ('DefaultConfigId', @v)",
                _conn);
            cmd.Parameters.AddWithValue("@v", id.ToString());
            cmd.ExecuteNonQuery();
        }

        // ── 内置配置种子 ────────────────────────────────────────────────────

        /// <summary>
        /// 确保内置配置存在并与代码定义保持同步。
        /// 优先从 {AppPath}/configs/builtin_configs.json 加载；文件不存在时回退到代码定义。
        /// </summary>
        public void SeedBuiltInConfigs()
        {
            var configs = TryLoadBuiltInConfigsFromFile() ?? BuiltInConfigs.GetAll();
            foreach (var config in configs)
                SeedSingleConfig(config);

            // 若 JSON 种子文件尚不存在，自动从内置定义生成，便于后续数据驱动维护
            EnsureBuiltInSeedFileExists();
        }

        private void EnsureBuiltInSeedFileExists()
        {
            try
            {
                string configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
                string seedFile = Path.Combine(configsDir, "builtin_configs.json");
                if (File.Exists(seedFile)) return;

                var builtInNames = new HashSet<string>(BuiltInConfigs.BuiltInNames);
                var all = GetAll();
                var builtInList = new List<ExtractionConfig>();
                foreach (var item in all)
                {
                    if (builtInNames.Contains(item.Name))
                    {
                        var cfg = GetById(item.Id);
                        if (cfg != null) builtInList.Add(cfg);
                    }
                }
                if (builtInList.Count == 0) return;

                Directory.CreateDirectory(configsDir);
                File.WriteAllText(seedFile, JsonConvert.SerializeObject(builtInList, Formatting.Indented));
            }
            catch { /* 种子文件写入失败不影响主流程 */ }
        }

        /// <summary>尝试从 configs/builtin_configs.json 加载内置配置（返回 null 表示文件不存在）</summary>
        private List<ExtractionConfig>? TryLoadBuiltInConfigsFromFile()
        {
            try
            {
                string configsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "configs");
                string seedFile = Path.Combine(configsDir, "builtin_configs.json");
                if (!File.Exists(seedFile)) return null;

                string json = File.ReadAllText(seedFile);
                var configs = JsonConvert.DeserializeObject<List<ExtractionConfig>>(json);
                return configs?.Count > 0 ? configs : null;
            }
            catch
            {
                return null;
            }
        }

        private void SeedSingleConfig(ExtractionConfig config)
        {
            PreserveUserSettings(config);
            string json = JsonConvert.SerializeObject(config, Formatting.None);
            using var cmd = new SQLiteCommand(
                @"INSERT INTO ExtractionConfig (ConfigName, ConfigJson)
                  VALUES (@name, @json)
                  ON CONFLICT(ConfigName) DO UPDATE
                    SET ConfigJson = excluded.ConfigJson,
                        UpdatedAt  = datetime('now')",
                _conn);
            cmd.Parameters.AddWithValue("@name", config.ConfigName);
            cmd.Parameters.AddWithValue("@json", json);
            cmd.ExecuteNonQuery();
        }

        /// <summary>将当前内置配置（含所有用户自定义配置）导出为 JSON 种子文件</summary>
        public void ExportAllConfigsToSeedFile(string outputPath)
        {
            var all = GetAll();
            var configs = new List<ExtractionConfig>();
            foreach (var item in all)
            {
                var cfg = GetById(item.Id);
                if (cfg != null) configs.Add(cfg);
            }
            string json = JsonConvert.SerializeObject(configs, Formatting.Indented);
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(outputPath, json);
        }

        /// <summary>
        /// 从数据库读取已保存的同名配置，将用户修改的运行时偏好（归一化选项等）
        /// 合并到即将写入的内置配置中，避免每次启动丢失用户设置。
        /// </summary>
        private void PreserveUserSettings(ExtractionConfig config)
        {
            using var readCmd = new SQLiteCommand(
                "SELECT ConfigJson FROM ExtractionConfig WHERE ConfigName=@name", _conn);
            readCmd.Parameters.AddWithValue("@name", config.ConfigName);
            var existingJson = readCmd.ExecuteScalar() as string;
            if (existingJson == null) return;

            var existing = JsonConvert.DeserializeObject<ExtractionConfig>(existingJson);
            if (existing == null) return;

            config.EnableValueNormalization = existing.EnableValueNormalization;
            if (existing.NormalizationOptions != null)
                config.NormalizationOptions = existing.NormalizationOptions;
        }

        public void Dispose() => _conn.Dispose();

        private static string GetEmbeddedSchema() => @"
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
