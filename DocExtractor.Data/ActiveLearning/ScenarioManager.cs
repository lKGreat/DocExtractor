using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using DocExtractor.Data.Repositories;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// 场景管理器：负责场景的 CRUD 和内置场景模板的初始化
    /// </summary>
    public class ScenarioManager
    {
        private readonly string _dbPath;

        public ScenarioManager(string dbPath)
        {
            _dbPath = dbPath;
        }

        private static readonly List<NlpScenario> BuiltInScenarios = new List<NlpScenario>
        {
            new NlpScenario
            {
                Name        = "协议字段提取",
                Description = "从协议规格文档中提取数值、单位、十六进制码、位域范围等字段信息",
                EntityTypes = new List<string> { "Value", "Unit", "HexCode", "Formula", "Enum", "Condition" },
                IsBuiltIn   = true
            },
            new NlpScenario
            {
                Name        = "产品参数提取",
                Description = "从产品规格书或技术文档中提取产品名称、规格参数、容差、物料等信息",
                EntityTypes = new List<string> { "ProductName", "Spec", "Tolerance", "Material", "Value", "Unit" },
                IsBuiltIn   = true
            },
            new NlpScenario
            {
                Name        = "自由文本提取",
                Description = "通用场景：支持用户自定义实体类型，适用于任意领域的关键信息提取",
                EntityTypes = new List<string> { "KeyInfo", "Person", "Organization", "Location", "Date", "Number" },
                IsBuiltIn   = true
            }
        };

        public void EnsureBuiltInScenarios()
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            var existing     = repo.GetAllScenarios();
            var existingNames = new HashSet<string>(existing.Select(s => s.Name), StringComparer.OrdinalIgnoreCase);

            foreach (var template in BuiltInScenarios)
            {
                if (!existingNames.Contains(template.Name))
                    repo.AddScenario(template);
            }
        }

        public List<NlpScenario> GetAllScenarios()
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetAllScenarios();
        }

        public NlpScenario? GetById(int id)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.GetScenarioById(id);
        }

        public int CreateScenario(string name, string description, List<string> entityTypes)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            return repo.AddScenario(new NlpScenario
            {
                Name        = name,
                Description = description,
                EntityTypes = entityTypes,
                IsBuiltIn   = false
            });
        }

        public void DeleteScenario(int id)
        {
            using var repo = new ActiveLearningRepository(_dbPath);
            repo.DeleteScenario(id);
        }

        /// <summary>返回场景实体类型的颜色映射（用于 UI 高亮）</summary>
        public static Dictionary<string, Color> GetEntityColors(NlpScenario scenario)
        {
            var palette = new[]
            {
                Color.FromArgb(255, 214, 102),
                Color.FromArgb(149, 225, 211),
                Color.FromArgb(255, 168, 168),
                Color.FromArgb(168, 199, 250),
                Color.FromArgb(200, 168, 250),
                Color.FromArgb(168, 250, 180),
                Color.FromArgb(250, 200, 168),
                Color.FromArgb(220, 220, 160),
            };

            var result = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < scenario.EntityTypes.Count; i++)
                result[scenario.EntityTypes[i]] = palette[i % palette.Length];

            return result;
        }
    }
}
