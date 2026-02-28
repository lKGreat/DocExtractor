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
                    SourceFileNormalized TEXT,
                    CreatedAt TEXT DEFAULT (datetime('now'))
                );
                CREATE INDEX IF NOT EXISTS idx_gik_group ON GroupItemKnowledge(GroupNameNormalized);
                CREATE INDEX IF NOT EXISTS idx_gik_src  ON GroupItemKnowledge(SourceFileNormalized);";
            cmd.ExecuteNonQuery();

            // 兼容旧表：补充 SourceFileNormalized 列（旧库可能没有）
            try
            {
                using var alter = _conn.CreateCommand();
                alter.CommandText = "ALTER TABLE GroupItemKnowledge ADD COLUMN SourceFileNormalized TEXT";
                alter.ExecuteNonQuery();
            }
            catch { /* 列已存在，忽略 */ }
        }

        /// <summary>
        /// 判断某文件是否已录入知识库
        /// </summary>
        public bool IsSourceFileLearned(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile)) return false;
            string norm = NormalizeSourceFile(sourceFile);
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM GroupItemKnowledge WHERE SourceFileNormalized=@s";
            cmd.Parameters.AddWithValue("@s", norm);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }

        /// <summary>
        /// 删除某文件贡献的所有知识库记录（为替换模式做准备）
        /// </summary>
        public int DeleteBySourceFile(string sourceFile)
        {
            if (string.IsNullOrWhiteSpace(sourceFile)) return 0;
            string norm = NormalizeSourceFile(sourceFile);
            using var cmd = _conn.CreateCommand();
            cmd.CommandText = "DELETE FROM GroupItemKnowledge WHERE SourceFileNormalized=@s";
            cmd.Parameters.AddWithValue("@s", norm);
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// 以"替换"模式批量写入一个来源文件的所有组及细则项。
        /// 先删除该文件的旧记录，再逐组写入，保证同文件不重复录入。
        /// </summary>
        /// <param name="recordsByGroup">分组后的记录字典：组名 → 记录列表</param>
        /// <param name="sourceFile">来源文件路径（用于去重索引）</param>
        /// <returns>写入的细则条数</returns>
        public int ReplaceSourceFileItems(
            IReadOnlyDictionary<string, IReadOnlyList<ExtractedRecord>> recordsByGroup,
            string sourceFile)
        {
            string normSrc = NormalizeSourceFile(sourceFile);

            using var tx = _conn.BeginTransaction();

            // 先清除该文件的旧数据
            using (var delCmd = _conn.CreateCommand())
            {
                delCmd.Transaction = tx;
                delCmd.CommandText = "DELETE FROM GroupItemKnowledge WHERE SourceFileNormalized=@s";
                delCmd.Parameters.AddWithValue("@s", normSrc);
                delCmd.ExecuteNonQuery();
            }

            int inserted = 0;
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO GroupItemKnowledge
                (GroupNameNormalized, ItemName, RequiredValue, OtherFieldsJson, SourceFile, SourceFileNormalized)
                VALUES (@gn, @item, @req, @other, @src, @nsrc)";

            var pGn   = cmd.Parameters.Add("@gn",   System.Data.DbType.String);
            var pItem = cmd.Parameters.Add("@item",  System.Data.DbType.String);
            var pReq  = cmd.Parameters.Add("@req",   System.Data.DbType.String);
            var pOther= cmd.Parameters.Add("@other", System.Data.DbType.String);
            var pSrc  = cmd.Parameters.Add("@src",   System.Data.DbType.String);
            var pNSrc = cmd.Parameters.Add("@nsrc",  System.Data.DbType.String);

            pSrc.Value  = sourceFile;
            pNSrc.Value = normSrc;

            foreach (var kv in recordsByGroup)
            {
                string normalizedGroup = NormalizeGroupName(kv.Key);
                pGn.Value = normalizedGroup;

                foreach (var r in kv.Value)
                {
                    string itemName = r.GetField("ItemName");
                    if (string.IsNullOrWhiteSpace(itemName)) continue;

                    pItem.Value = itemName.Trim();
                    pReq.Value  = (object)r.GetField("RequiredValue") ?? DBNull.Value;

                    var otherFields = new Dictionary<string, string>();
                    foreach (var field in r.Fields)
                    {
                        if (field.Key != "GroupName" && field.Key != "ItemName"
                            && field.Key != "RequiredValue" && !string.IsNullOrWhiteSpace(field.Value))
                            otherFields[field.Key] = field.Value;
                    }
                    pOther.Value = otherFields.Count > 0
                        ? (object)JsonConvert.SerializeObject(otherFields)
                        : DBNull.Value;

                    cmd.ExecuteNonQuery();
                    inserted++;
                }
            }

            tx.Commit();
            return inserted;
        }

        /// <summary>
        /// 批量写入一个组的细则项（兼容旧接口，不清除旧数据）
        /// </summary>
        public void AddGroupItems(string groupName, IReadOnlyList<ExtractedRecord> records)
        {
            string normalized = NormalizeGroupName(groupName);

            using var tx = _conn.BeginTransaction();
            using var cmd = _conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO GroupItemKnowledge
                (GroupNameNormalized, ItemName, RequiredValue, OtherFieldsJson, SourceFile, SourceFileNormalized)
                VALUES (@gn, @item, @req, @other, @src, @nsrc)";

            var pGn   = cmd.Parameters.Add("@gn",   System.Data.DbType.String);
            var pItem = cmd.Parameters.Add("@item",  System.Data.DbType.String);
            var pReq  = cmd.Parameters.Add("@req",   System.Data.DbType.String);
            var pOther= cmd.Parameters.Add("@other", System.Data.DbType.String);
            var pSrc  = cmd.Parameters.Add("@src",   System.Data.DbType.String);
            var pNSrc = cmd.Parameters.Add("@nsrc",  System.Data.DbType.String);

            string srcFile = records.Count > 0 ? records[0].SourceFile : string.Empty;
            pSrc.Value  = srcFile;
            pNSrc.Value = NormalizeSourceFile(srcFile);

            foreach (var r in records)
            {
                string itemName = r.GetField("ItemName");
                if (string.IsNullOrWhiteSpace(itemName)) continue;

                pGn.Value   = normalized;
                pItem.Value = itemName.Trim();
                pReq.Value  = (object)r.GetField("RequiredValue") ?? DBNull.Value;

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

        /// <summary>
        /// 归一化来源文件路径（转小写、统一斜杠），用于文件去重索引
        /// </summary>
        public static string NormalizeSourceFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            return path.Trim().ToLowerInvariant().Replace('\\', '/');
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
