using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;

namespace DocExtractor.Core.Splitting
{
    /// <summary>
    /// 时间轴展开拆分器：检测多步序列、跳变（带/不带公差）、阈值模式，
    /// 将一条记录拆分为多行，每行注入清洗后的值和时间轴字段。
    /// </summary>
    public class TimeAxisSplitter : IRecordSplitter
    {
        public SplitType SupportedType => SplitType.TimeAxisExpand;

        // ── Pattern 1: Multi-step sequence ──────────────────────────────────
        // 1.8A（10s）—>2.0A（10s）—>2.2A（20s）—>2.8A（10s）—>3.5A（10s）—>4.5A
        private static readonly Regex ArrowSplitPattern = new Regex(
            @"[—\-]>|→",
            RegexOptions.Compiled);

        private static readonly Regex StepValuePattern = new Regex(
            @"([+-]?\d+\.?\d*)",
            RegexOptions.Compiled);

        private static readonly Regex StepTimePattern = new Regex(
            @"[（(]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*后?\s*[）)]",
            RegexOptions.Compiled);

        // ── Pattern 2: Transition with tolerance ────────────────────────────
        // 0A→1.5A±0.1A（235s后）
        private static readonly Regex TransitionTolerancePattern = new Regex(
            @"^\s*([+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*(?:[—\-]>|→)\s*([+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*[±]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*[（(]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*后?\s*[）)]",
            RegexOptions.Compiled);

        // ── Pattern 3: Simple transition ────────────────────────────────────
        // 0x5→0xA（235s后）, 0x55→0xAA（235s后）, 0V→220V±10V（235s后） without ±
        private static readonly Regex TransitionSimplePattern = new Regex(
            @"^\s*(0x[0-9a-fA-F]+|[+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*(?:[—\-]>|→)\s*(0x[0-9a-fA-F]+|[+-]?\d+\.?\d*)\s*[a-zA-Z]*\s*[（(]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*后?\s*[）)]",
            RegexOptions.Compiled);

        // ── Pattern 4: Threshold with time ──────────────────────────────────
        // ＜24.5V（2s后）
        private static readonly Regex ThresholdTimePattern = new Regex(
            @"^\s*[＜<≤]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*[（(]\s*(\d+\.?\d*)\s*[a-zA-Z]*\s*后?\s*[）)]",
            RegexOptions.Compiled);

        public IEnumerable<ExtractedRecord> Split(ExtractedRecord record, SplitRule rule)
        {
            string fieldName = rule.TriggerColumn;
            if (string.IsNullOrWhiteSpace(fieldName) ||
                !record.Fields.TryGetValue(fieldName, out var value) ||
                string.IsNullOrWhiteSpace(value))
            {
                yield return record;
                yield break;
            }

            string timeField = string.IsNullOrWhiteSpace(rule.TimeAxisFieldName)
                ? "TimeAxis"
                : rule.TimeAxisFieldName;

            var steps = TryTransitionWithTolerance(value)
                     ?? TryMultiStepSequence(value, rule.DefaultTolerance, rule.DefaultTimeValue)
                     ?? TryTransitionSimple(value)
                     ?? TryThresholdWithTime(value);

            if (steps == null || steps.Count == 0)
            {
                yield return record;
                yield break;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                var newRecord = CloneRecord(record);
                newRecord.Id = record.Id + $"_ta{i}";
                newRecord.Fields[fieldName] = steps[i].Value;
                newRecord.Fields[timeField] = steps[i].Time;
                yield return newRecord;
            }
        }

        // ── Pattern handlers ────────────────────────────────────────────────

        private static List<StepResult> TryMultiStepSequence(
            string input, double tolerance, double defaultTime)
        {
            var segments = ArrowSplitPattern.Split(input);
            if (segments.Length < 2)
                return null;

            var results = new List<StepResult>();
            foreach (var seg in segments)
            {
                string trimmed = seg.Trim();
                if (trimmed.Length == 0) continue;

                // Remove trailing annotations like （示波器保留波形六档）
                var valueMatch = StepValuePattern.Match(trimmed);
                if (!valueMatch.Success) continue;

                string numStr = valueMatch.Groups[1].Value;

                var timeMatch = StepTimePattern.Match(trimmed);
                string time = timeMatch.Success
                    ? timeMatch.Groups[1].Value
                    : defaultTime.ToString(CultureInfo.InvariantCulture);

                string finalValue;
                if (tolerance > 0 && double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                {
                    double lo = num - tolerance;
                    double hi = num + tolerance;
                    finalValue = FormatNumber(lo) + "/" + FormatNumber(hi);
                }
                else
                {
                    finalValue = numStr;
                }

                results.Add(new StepResult(finalValue, time));
            }

            return results.Count >= 2 ? results : null;
        }

        private static List<StepResult> TryTransitionWithTolerance(string input)
        {
            var m = TransitionTolerancePattern.Match(input);
            if (!m.Success) return null;

            string firstVal = m.Groups[1].Value;
            string centerStr = m.Groups[2].Value;
            string tolStr = m.Groups[3].Value;
            string time = m.Groups[4].Value;

            if (!double.TryParse(centerStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double center) ||
                !double.TryParse(tolStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double tol))
                return null;

            string rangeValue = FormatNumber(center - tol) + "/" + FormatNumber(center + tol);

            return new List<StepResult>
            {
                new StepResult(firstVal, "0"),
                new StepResult(rangeValue, time)
            };
        }

        private static List<StepResult> TryTransitionSimple(string input)
        {
            var m = TransitionSimplePattern.Match(input);
            if (!m.Success) return null;

            // Skip if tolerance pattern would match (avoid double-matching)
            if (input.Contains("±")) return null;

            return new List<StepResult>
            {
                new StepResult(m.Groups[1].Value, "0"),
                new StepResult(m.Groups[2].Value, m.Groups[3].Value)
            };
        }

        private static List<StepResult> TryThresholdWithTime(string input)
        {
            var m = ThresholdTimePattern.Match(input);
            if (!m.Success) return null;

            string threshold = m.Groups[1].Value;
            string time = m.Groups[2].Value;

            return new List<StepResult>
            {
                new StepResult("0/" + threshold, time)
            };
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string FormatNumber(double value)
        {
            string s = value.ToString("0.####", CultureInfo.InvariantCulture);
            return s;
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

        private struct StepResult
        {
            public readonly string Value;
            public readonly string Time;

            public StepResult(string value, string time)
            {
                Value = value;
                Time = time;
            }
        }
    }
}
