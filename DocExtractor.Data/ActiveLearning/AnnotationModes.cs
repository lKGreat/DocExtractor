using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// Supported annotation modes in NLP lab.
    /// </summary>
    public enum AnnotationMode
    {
        SpanEntity,
        KvSchema,
        EnumBitfield,
        Relation,
        Sequence
    }

    public static class AnnotationModeHelper
    {
        public static List<AnnotationMode> ParseModes(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<AnnotationMode> { AnnotationMode.SpanEntity };

            try
            {
                var names = JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
                var modes = new List<AnnotationMode>();
                foreach (var name in names)
                {
                    if (Enum.TryParse(name, ignoreCase: true, out AnnotationMode mode))
                        modes.Add(mode);
                }
                if (modes.Count == 0)
                    modes.Add(AnnotationMode.SpanEntity);
                return modes.Distinct().ToList();
            }
            catch
            {
                return new List<AnnotationMode> { AnnotationMode.SpanEntity };
            }
        }

        public static string ToJson(IEnumerable<AnnotationMode>? modes)
        {
            var effective = (modes ?? Array.Empty<AnnotationMode>())
                .Distinct()
                .ToList();
            if (effective.Count == 0)
                effective.Add(AnnotationMode.SpanEntity);
            return JsonConvert.SerializeObject(effective.Select(m => m.ToString()).ToList());
        }

        public static string GetDisplayName(AnnotationMode mode)
        {
            return mode switch
            {
                AnnotationMode.SpanEntity => "实体片段",
                AnnotationMode.KvSchema => "结构化键值",
                AnnotationMode.EnumBitfield => "位段枚举",
                AnnotationMode.Relation => "关系",
                AnnotationMode.Sequence => "时序",
                _ => mode.ToString()
            };
        }
    }

    /// <summary>
    /// Basic template shape used by structured extraction modes.
    /// </summary>
    public class AnnotationTemplateDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0";
        public List<string> DeviceLevels { get; set; } = new List<string> { "设备", "子系统", "参数", "取值" };
        public List<string> RelationTypes { get; set; } = new List<string>();
        public List<string> SequenceFields { get; set; } = new List<string> { "步骤", "事件", "条件", "状态" };
    }

    public static class AnnotationTemplateFactory
    {
        public static string BuildDefaultTemplateJson(string scenarioName)
        {
            var template = new AnnotationTemplateDefinition
            {
                Name = string.IsNullOrWhiteSpace(scenarioName) ? "默认模板" : $"{scenarioName}模板",
                RelationTypes = new List<string> { "包含", "依赖", "触发", "影响" }
            };
            return JsonConvert.SerializeObject(template);
        }
    }
}
