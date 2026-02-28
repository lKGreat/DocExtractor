using System;
using System.Collections.Generic;
using System.Data.SQLite;
using DocExtractor.Core.Models;
using Newtonsoft.Json;

namespace DocExtractor.Data.Repositories
{
    /// <summary>
    /// 组名→细则项知识库，支持自动学习和推荐查询
    /// </summary>
    public class GroupKnowledgeRepository : IDisposable
    {
        private readonly SQLiteConnection _conn;

        public GroupKnowledgeRepository(string dbPath)
        {
            _conn = new SQLiteConnection($"Data Source={dbPath};Version=3;");
            _conn.Open();
            EnsureTable();
        }

        private void EnsureTable()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS GroupItemKnowledge (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupNameNormalized TEXT NOT NULL,
                    ItemName TEXT NOT NULL,
                    RequiredValue TEXT,
                    OtherFieldsJson TEXT,
                    SourceFile TEXT,
                    CreatedAt TEXT DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS idx_gik_group ON GroupItemKnowledge(GroupNameNormalized);";
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 批量写入一个组的细则项（从抽取结果自动学习）
        /// </summary>
        public void AddGroupItems(string groupName, IReadOnlyList<ExtractedRecord> records)
        {
            string normalized = NormalizeGroupName(groupName);

            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO GroupItemKnowledge
                (GroupNameNormalized, ItemName, RequiredValue, OtherFieldsJson, SourceFile)
                VALUES (@gn, @item, @req, @other, @src)";

            var pGn = cmd.Parameters.Add("@gn", System.Data.DbType.String);
            var pItem = cmd.Parameters.Add("@item", System.Data.DbType.String);
            var pReq = cmd.Parameters.Add("@req", System.Data.DbType.String);
            var pOther = cmd.Parameters.Add("@other", System.Data.DbType.String);
            var pSrc = cmd.Parameters.Add("@src", System.Data.DbType.String);

            foreach (var r in records)
            {
                string itemName = r.GetField("ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                pGn.Value = normalized;
                pItem.Value = itemName.Trim();
                pReq.Value = (object)r.GetField("RequiredValue") ?? DBNull.Value;

                // 保存其他字段为 JSON
                var otherFields = new Dictionary<string, string>();
                foreach (var kv in r.Fields)
                {
                    if (kv.Key != "GroupName" && kv.Key != "ItemName" && kv.Key != "RequiredValue"
                        && !string.IsNullOrWhiteSpace(kv.Value))
                        otherFields[kv.Key] = kv.Value;
                }
                pOther.Value = otherFields.Count > 0
                    ? (object)JsonConvert.SerializeObject(otherFields)
                    : DBNull.Value;
                pSrc.Value = (object)r.SourceFile ?? DBNull.Value;

                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }

        /// <summary>获取所有唯一的归一化组名</summary>
        public List<string> GetDistinctGroupNames()
        {
            var result = new List<string>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT GroupNameNormalized FROM GroupItemKnowledge ORDER BY GroupNameNormalized";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
                result.Add(reader.GetString(0));
            return result;
        }

        /// <summary>获取某组下所有细则项</summary>
        public List<GroupItem> GetItemsForGroup(string groupName)
        {
            string normalized = NormalizeGroupName(groupName);
            var result = new List<GroupItem>();
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = @"SELECT ItemName, RequiredValue, OtherFieldsJson, SourceFile
                FROM GroupItemKnowledge WHERE GroupNameNormalized = @gn";
            cmd.Parameters.AddWithValue("@gn", normalized);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new GroupItem
                {
                    ItemName = reader.GetString(0),
                    RequiredValue = reader.IsDBNull(1) ? null : reader.GetString(1),
                    OtherFieldsJson = reader.IsDBNull(2) ? null : reader.GetString(2),
                    SourceFile = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }
            return result;
        }

        /// <summary>知识库总记录数</summary>
        public int GetKnowledgeCount()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM GroupItemKnowledge";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        /// <summary>清空知识库</summary>
        public void ClearKnowledge()
        {
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GroupItemKnowledge";
            cmd.ExecuteNonQuery();
        }

        /// <summary>归一化组名：去除首尾空白、序号前缀等</summary>
        public static string NormalizeGroupName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            string trimmed = name.Trim();
            // 去除常见数字序号前缀：如 "3.2 测试步骤" → "测试步骤"
            int i = 0;
            while (i < trimmed.Length && (char.IsDigit(trimmed[i]) || trimmed[i] == '.' || trimmed[i] == ' '
                                           || trimmed[i] == '\u3001' || trimmed[i] == '\uff0e'))
                i++;
            string result = i < trimmed.Length ? trimmed.Substring(i).Trim() : trimmed;
            return result.Length > 0 ? result : trimmed;
        }

        public void Dispose()
        {
            _conn?.Close();
            _conn?.Dispose();
        }
    }

    /// <summary>知识库中的单条细则项</summary>
    public class GroupItem
    {
        public string ItemName { get; set; } = string.Empty;
        public string RequiredValue { get; set; }
        public string OtherFieldsJson { get; set; }
        public string SourceFile { get; set; }
    }
}
