using System;
using System.Collections.Generic;
using System.Linq;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Linking
{
    /// <summary>
    /// 跨表关联器：识别值域重叠的关联键并将从表字段 LEFT JOIN 到主表。
    /// </summary>
    public class CrossTableLinker
    {
        public List<ExtractedRecord> Link(
            IReadOnlyList<ExtractedRecord> records,
            double minOverlap = 0.6)
        {
            if (records.Count == 0) return new List<ExtractedRecord>();

            var result = records.Select(CloneRecord).ToList();
            var tableGroups = result.GroupBy(r => r.SourceTableIndex).ToList();
            if (tableGroups.Count < 2) return result;

            for (int i = 0; i < tableGroups.Count; i++)
            {
                for (int j = i + 1; j < tableGroups.Count; j++)
                {
                    var tableA = tableGroups[i].ToList();
                    var tableB = tableGroups[j].ToList();
                    var candidate = FindBestJoinKey(tableA, tableB, minOverlap);
                    if (candidate == null) continue;

                    var main = tableA.Count >= tableB.Count ? tableA : tableB;
                    var side = tableA.Count >= tableB.Count ? tableB : tableA;
                    ApplyJoin(main, side, candidate.KeyField);
                }
            }

            return result;
        }

        private static JoinCandidate? FindBestJoinKey(
            IReadOnlyList<ExtractedRecord> tableA,
            IReadOnlyList<ExtractedRecord> tableB,
            double minOverlap)
        {
            var fieldsA = GetNonEmptyFields(tableA);
            var fieldsB = GetNonEmptyFields(tableB);
            var commonFields = fieldsA.Intersect(fieldsB, StringComparer.OrdinalIgnoreCase).ToList();

            JoinCandidate? best = null;
            foreach (var field in commonFields)
            {
                var setA = new HashSet<string>(
                    tableA.Select(r => r.GetField(field)).Where(v => !string.IsNullOrWhiteSpace(v)),
                    StringComparer.OrdinalIgnoreCase);
                var setB = new HashSet<string>(
                    tableB.Select(r => r.GetField(field)).Where(v => !string.IsNullOrWhiteSpace(v)),
                    StringComparer.OrdinalIgnoreCase);

                if (setA.Count == 0 || setB.Count == 0) continue;
                int intersection = setA.Count(v => setB.Contains(v));
                int union = setA.Count + setB.Count - intersection;
                double overlap = union == 0 ? 0 : (double)intersection / union;

                if (overlap >= minOverlap && (best == null || overlap > best.Overlap))
                {
                    best = new JoinCandidate { KeyField = field, Overlap = overlap };
                }
            }

            return best;
        }

        private static HashSet<string> GetNonEmptyFields(IReadOnlyList<ExtractedRecord> records)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in records)
            {
                foreach (var kv in record.Fields)
                {
                    if (!string.IsNullOrWhiteSpace(kv.Value))
                        fields.Add(kv.Key);
                }
            }
            return fields;
        }

        private static void ApplyJoin(List<ExtractedRecord> main, List<ExtractedRecord> side, string keyField)
        {
            var sideLookup = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in side)
            {
                string key = record.GetField(keyField);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!sideLookup.ContainsKey(key))
                    sideLookup[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var kv in record.Fields)
                {
                    if (string.Equals(kv.Key, keyField, StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.IsNullOrWhiteSpace(kv.Value)) continue;
                    sideLookup[key][kv.Key] = kv.Value;
                }
            }

            foreach (var record in main)
            {
                string key = record.GetField(keyField);
                if (string.IsNullOrWhiteSpace(key)) continue;
                if (!sideLookup.TryGetValue(key, out var extraFields)) continue;

                foreach (var kv in extraFields)
                {
                    if (!record.Fields.ContainsKey(kv.Key) || string.IsNullOrWhiteSpace(record.Fields[kv.Key]))
                        record.Fields[kv.Key] = kv.Value;
                }
            }
        }

        private static ExtractedRecord CloneRecord(ExtractedRecord src)
        {
            return new ExtractedRecord
            {
                Id = src.Id,
                SourceFile = src.SourceFile,
                SourceTableIndex = src.SourceTableIndex,
                SourceRowIndex = src.SourceRowIndex,
                Fields = new Dictionary<string, string>(src.Fields),
                RawValues = new Dictionary<string, string>(src.RawValues),
                IsComplete = src.IsComplete,
                Warnings = new List<string>(src.Warnings)
            };
        }

        private class JoinCandidate
        {
            public string KeyField { get; set; } = string.Empty;
            public double Overlap { get; set; }
        }
    }
}
