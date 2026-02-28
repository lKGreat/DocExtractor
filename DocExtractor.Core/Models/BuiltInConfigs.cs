using System.Collections.Generic;

namespace DocExtractor.Core.Models
{
    /// <summary>
    /// 内置抽取配置模板：基于遥测/遥控领域的预定义字段配置
    /// </summary>
    public static class BuiltInConfigs
    {
        /// <summary>内置配置名称列表，用于判断是否可删除</summary>
        public static readonly HashSet<string> BuiltInNames = new HashSet<string>
        {
            "遥测参数抽取",
            "遥控指令抽取",
            "遥控参数抽取",
            "测试细则抽取"
        };

        /// <summary>获取所有内置配置</summary>
        public static List<ExtractionConfig> GetAll() => new List<ExtractionConfig>
        {
            CreateTelemetryConfig(),
            CreateTelecommandConfig(),
            CreateTelecommandParamsConfig(),
            CreateTestSpecConfig()
        };

        /// <summary>
        /// 遥测参数抽取配置（15 字段）
        /// 适用于遥测数据表，包含 APID、起始字节、波道名称、公式系数等
        /// </summary>
        public static ExtractionConfig CreateTelemetryConfig() => new ExtractionConfig
        {
            ConfigName = "遥测参数抽取",
            HeaderRowCount = 1,
            ColumnMatch = ColumnMatchMode.HybridMlFirst,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "Index", DisplayName = "序号",
                    KnownColumnVariants = new List<string> { "序号", "No.", "编号", "NO" } },
                new FieldDefinition { FieldName = "System", DisplayName = "所属系统",
                    KnownColumnVariants = new List<string> { "所属系统", "系统", "System", "子系统" } },
                new FieldDefinition { FieldName = "APID", DisplayName = "APID值", DataType = FieldDataType.HexCode,
                    KnownColumnVariants = new List<string> { "APID值", "APID", "应用标识", "应用过程标识" }, IsRequired = true },
                new FieldDefinition { FieldName = "StartByte", DisplayName = "起始字节", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "起始字节", "起始字节序号", "开始字节", "字节偏移" }, IsRequired = true },
                new FieldDefinition { FieldName = "BitOffset", DisplayName = "起始位",
                    KnownColumnVariants = new List<string> { "起始位", "起始比特", "比特偏移" } },
                new FieldDefinition { FieldName = "BitLength", DisplayName = "位长度", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "位长度", "字节长度", "比特数", "长度", "数据长度" }, IsRequired = true },
                new FieldDefinition { FieldName = "ChannelName", DisplayName = "波道名称",
                    KnownColumnVariants = new List<string> { "波道名称", "参数名称", "通道名称", "名称", "遥测参数名称" }, IsRequired = true },
                new FieldDefinition { FieldName = "TelemetryCode", DisplayName = "遥测代号",
                    KnownColumnVariants = new List<string> { "遥测代号", "参数代号", "代号", "标识", "参数标识" }, IsRequired = true },
                new FieldDefinition { FieldName = "Endianness", DisplayName = "字节端序",
                    KnownColumnVariants = new List<string> { "字节端序", "端序", "大小端", "字节序" } },
                new FieldDefinition { FieldName = "FormulaType", DisplayName = "公式类型",
                    KnownColumnVariants = new List<string> { "公式类型", "转换类型", "类型", "换算方式" } },
                new FieldDefinition { FieldName = "CoeffA", DisplayName = "系数A", DataType = FieldDataType.Decimal,
                    KnownColumnVariants = new List<string> { "A", "系数A", "公式系数/A", "系数a" } },
                new FieldDefinition { FieldName = "CoeffB", DisplayName = "系数B", DataType = FieldDataType.Decimal,
                    KnownColumnVariants = new List<string> { "B", "系数B", "公式系数/B", "系数b" } },
                new FieldDefinition { FieldName = "Precision", DisplayName = "小数位数", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "小数位数", "精度", "小数位" } },
                new FieldDefinition { FieldName = "Unit", DisplayName = "量纲",
                    KnownColumnVariants = new List<string> { "量纲", "单位", "工程量纲", "物理量纲" } },
                new FieldDefinition { FieldName = "EnumMap", DisplayName = "枚举解译",
                    KnownColumnVariants = new List<string> { "枚举解译", "离散值", "枚举值", "状态描述", "枚举定义" },
                    DataType = FieldDataType.Enumeration }
            },
            SplitRules = new List<SplitRule>
            {
                new SplitRule
                {
                    RuleName = "枚举值展开",
                    Type = SplitType.SubTableExpand,
                    TriggerColumn = "EnumMap",
                    InheritParentFields = true,
                    Priority = 10
                }
            }
        };

        /// <summary>
        /// 遥控指令抽取配置（10 字段）
        /// 适用于遥控指令格式表，包含起始字节、数据类型、转换规则等
        /// </summary>
        public static ExtractionConfig CreateTelecommandConfig() => new ExtractionConfig
        {
            ConfigName = "遥控指令抽取",
            HeaderRowCount = 1,
            ColumnMatch = ColumnMatchMode.HybridMlFirst,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "System", DisplayName = "所属系统",
                    KnownColumnVariants = new List<string> { "所属系统", "系统", "System", "子系统" } },
                new FieldDefinition { FieldName = "StartByte", DisplayName = "起始字节", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "起始字节", "起始字节序号", "开始字节", "字节偏移" }, IsRequired = true },
                new FieldDefinition { FieldName = "BitOffset", DisplayName = "起始位",
                    KnownColumnVariants = new List<string> { "起始位", "起始比特", "比特偏移" } },
                new FieldDefinition { FieldName = "BitLength", DisplayName = "字节长度/位长度", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "字节长度", "位长度", "比特数", "长度", "数据长度" }, IsRequired = true },
                new FieldDefinition { FieldName = "Unit", DisplayName = "量纲",
                    KnownColumnVariants = new List<string> { "量纲", "单位", "工程量纲" } },
                new FieldDefinition { FieldName = "IsVisible", DisplayName = "是否显示", DataType = FieldDataType.Boolean,
                    KnownColumnVariants = new List<string> { "是否显示", "显示", "可见" } },
                new FieldDefinition { FieldName = "Endianness", DisplayName = "字节端序",
                    KnownColumnVariants = new List<string> { "字节端序", "端序", "大小端", "字节序" } },
                new FieldDefinition { FieldName = "DefaultContent", DisplayName = "默认内容",
                    KnownColumnVariants = new List<string> { "默认内容", "默认值", "缺省值", "初始值" } },
                new FieldDefinition { FieldName = "DataType", DisplayName = "数据类型",
                    KnownColumnVariants = new List<string> { "数据类型", "类型", "格式", "数据格式" } },
                new FieldDefinition { FieldName = "ConvertRule", DisplayName = "转换规则",
                    KnownColumnVariants = new List<string> { "转换规则", "转换方式", "换算规则", "公式" } }
            }
        };

        /// <summary>
        /// 遥控参数抽取配置（10 字段）
        /// 适用于遥控参数定义表，包含参数ID、输入类型、取值范围等
        /// </summary>
        public static ExtractionConfig CreateTelecommandParamsConfig() => new ExtractionConfig
        {
            ConfigName = "遥控参数抽取",
            HeaderRowCount = 1,
            ColumnMatch = ColumnMatchMode.HybridMlFirst,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "ParamId", DisplayName = "参数ID",
                    KnownColumnVariants = new List<string> { "参数ID", "参数编号", "参数标识", "ID" }, IsRequired = true },
                new FieldDefinition { FieldName = "ParamName", DisplayName = "参数名称",
                    KnownColumnVariants = new List<string> { "参数名称", "名称", "参数名" }, IsRequired = true },
                new FieldDefinition { FieldName = "ParamDesc", DisplayName = "参数描述",
                    KnownColumnVariants = new List<string> { "参数描述", "描述", "说明", "备注" } },
                new FieldDefinition { FieldName = "StartByte", DisplayName = "起始字节", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "起始字节", "字节位置", "偏移" } },
                new FieldDefinition { FieldName = "Length", DisplayName = "长度", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "长度", "字节长度", "数据长度", "位长度" } },
                new FieldDefinition { FieldName = "InputType", DisplayName = "输入类型",
                    KnownColumnVariants = new List<string> { "输入类型", "输入方式", "编辑类型" } },
                new FieldDefinition { FieldName = "DataFormat", DisplayName = "数据格式",
                    KnownColumnVariants = new List<string> { "数据格式", "格式", "数据类型", "类型" } },
                new FieldDefinition { FieldName = "DefaultValue", DisplayName = "默认值",
                    KnownColumnVariants = new List<string> { "默认值", "缺省值", "初始值" } },
                new FieldDefinition { FieldName = "Options", DisplayName = "选项值",
                    KnownColumnVariants = new List<string> { "选项值", "可选值", "枚举值", "选项" },
                    DataType = FieldDataType.Enumeration },
                new FieldDefinition { FieldName = "ValueRange", DisplayName = "取值范围",
                    KnownColumnVariants = new List<string> { "取值范围", "范围", "有效范围", "值域" } }
            }
        };
        /// <summary>
        /// 测试细则抽取配置（5 字段）
        /// 适用于内部测试细则文档，表格含 序号/名称/要求值/检测值/合格结论
        /// 同时覆盖变体列名：上注值、显示值、实际值、除气时长等
        /// </summary>
        public static ExtractionConfig CreateTestSpecConfig() => new ExtractionConfig
        {
            ConfigName = "测试细则抽取",
            HeaderRowCount = 1,
            ColumnMatch = ColumnMatchMode.HybridRuleFirst,
            Fields = new List<FieldDefinition>
            {
                new FieldDefinition { FieldName = "Index", DisplayName = "序号", DataType = FieldDataType.Integer,
                    KnownColumnVariants = new List<string> { "序号", "No.", "编号" } },
                new FieldDefinition { FieldName = "ItemName", DisplayName = "名称",
                    KnownColumnVariants = new List<string> { "名称", "项目", "检查项目", "测试项目", "参数名称", "检测项" },
                    IsRequired = true },
                new FieldDefinition { FieldName = "RequiredValue", DisplayName = "要求值",
                    KnownColumnVariants = new List<string> { "要求值", "规定值", "指标值", "上注值", "设定值", "标准值" },
                    IsRequired = true },
                new FieldDefinition { FieldName = "MeasuredValue", DisplayName = "检测值",
                    KnownColumnVariants = new List<string> { "检测值", "测试值", "实际值", "显示值", "测量值", "读数" } },
                new FieldDefinition { FieldName = "Conclusion", DisplayName = "合格结论",
                    KnownColumnVariants = new List<string> { "合格结论", "结论", "判定", "是否合格", "结果", "除气时长" },
                    DataType = FieldDataType.Text }
            }
        };
    }
}
