using System;
using System.Collections.Generic;
using DocExtractor.Data.Repositories;
using Newtonsoft.Json;

namespace DocExtractor.Data.ActiveLearning
{
    /// <summary>
    /// 产品参数提取场景的种子训练数据
    /// </summary>
    public static class ProductParameterSeedData
    {
        /// <summary>
        /// 为"产品参数提取"场景插入 30 条训练样本
        /// </summary>
        public static int SeedProductParameterSamples(string dbPath)
        {
            using var repo = new ActiveLearningRepository(dbPath);
            var scenarios = repo.GetAllScenarios();
            var productScenario = scenarios.Find(s => string.Equals(s.Name, "产品参数提取", StringComparison.OrdinalIgnoreCase));
            if (productScenario == null)
                return 0;

            int scenarioId = productScenario.Id;
            int inserted = 0;

            foreach (var (rawText, spans) in GetSeedSamples())
            {
                var existing = repo.GetAnnotatedTexts(scenarioId)
                    .Find(t => t.RawText.Equals(rawText, StringComparison.Ordinal));
                if (existing != null) continue;

                var anns = new List<ActiveEntityAnnotation>();
                foreach (var (entityType, text) in spans)
                {
                    int idx = rawText.IndexOf(text, StringComparison.Ordinal);
                    if (idx >= 0)
                        anns.Add(new ActiveEntityAnnotation
                        {
                            StartIndex = idx,
                            EndIndex = idx + text.Length - 1,
                            EntityType = entityType,
                            Text = text,
                            Confidence = 1f,
                            IsManual = true
                        });
                }

                repo.AddAnnotatedText(new NlpAnnotatedText
                {
                    ScenarioId = scenarioId,
                    RawText = rawText,
                    AnnotationsJson = JsonConvert.SerializeObject(anns),
                    Source = "seed_product_param",
                    AnnotationMode = AnnotationMode.SpanEntity.ToString(),
                    StructuredAnnotationsJson = "{}",
                    ConfidenceScore = 1f,
                    IsVerified = true
                });
                inserted++;
            }

            return inserted;
        }

        /// <summary>
        /// 每条：(RawText, List of (EntityType, Text))
        /// </summary>
        private static IEnumerable<(string RawText, List<(string EntityType, string Text)> Spans)> GetSeedSamples()
        {
            yield return ("产品型号：XYZ-100，额定电压12V，电流范围0-2A，精度±0.5%",
                new List<(string, string)> { ("ProductName", "XYZ-100"), ("Spec", "额定电压"), ("Value", "12"), ("Unit", "V"), ("Spec", "电流范围"), ("Value", "0-2"), ("Unit", "A"), ("Tolerance", "±0.5%") });

            yield return ("型号ABC-200，工作电压24V DC，额定电流5A，功率120W",
                new List<(string, string)> { ("ProductName", "ABC-200"), ("Spec", "工作电压"), ("Value", "24"), ("Unit", "V"), ("Spec", "额定电流"), ("Value", "5"), ("Unit", "A"), ("Spec", "功率"), ("Value", "120"), ("Unit", "W") });

            yield return ("产品名称：精密电阻R-330，阻值10kΩ，容差±1%，功率0.25W",
                new List<(string, string)> { ("ProductName", "精密电阻R-330"), ("Spec", "阻值"), ("Value", "10k"), ("Unit", "Ω"), ("Tolerance", "±1%"), ("Spec", "功率"), ("Value", "0.25"), ("Unit", "W") });

            yield return ("电容C-100，容量100μF，耐压50V，材质陶瓷",
                new List<(string, string)> { ("ProductName", "电容C-100"), ("Spec", "容量"), ("Value", "100"), ("Unit", "μF"), ("Spec", "耐压"), ("Value", "50"), ("Unit", "V"), ("Material", "陶瓷") });

            yield return ("电感L-50，电感量10mH，额定电流2A，DCR≤0.5Ω",
                new List<(string, string)> { ("ProductName", "电感L-50"), ("Spec", "电感量"), ("Value", "10"), ("Unit", "mH"), ("Spec", "额定电流"), ("Value", "2"), ("Unit", "A"), ("Tolerance", "≤0.5"), ("Unit", "Ω") });

            yield return ("二极管D1N4148，正向电压0.7V，反向耐压100V",
                new List<(string, string)> { ("ProductName", "二极管D1N4148"), ("Spec", "正向电压"), ("Value", "0.7"), ("Unit", "V"), ("Spec", "反向耐压"), ("Value", "100"), ("Unit", "V") });

            yield return ("三极管2N2222，Vceo 40V，Ic 800mA，hFE 100-300",
                new List<(string, string)> { ("ProductName", "三极管2N2222"), ("Spec", "Vceo"), ("Value", "40"), ("Unit", "V"), ("Spec", "Ic"), ("Value", "800"), ("Unit", "mA"), ("Spec", "hFE"), ("Value", "100-300") });

            yield return ("运放OP07，供电±15V，失调电压75μV，温漂0.7μV/℃",
                new List<(string, string)> { ("ProductName", "运放OP07"), ("Spec", "供电"), ("Value", "±15"), ("Unit", "V"), ("Spec", "失调电压"), ("Value", "75"), ("Unit", "μV"), ("Tolerance", "0.7μV/℃") });

            yield return ("稳压器LM7805，输入7-25V，输出5V 1A，压差2V",
                new List<(string, string)> { ("ProductName", "稳压器LM7805"), ("Spec", "输入"), ("Value", "7-25"), ("Unit", "V"), ("Spec", "输出"), ("Value", "5"), ("Unit", "V"), ("Value", "1"), ("Unit", "A"), ("Spec", "压差"), ("Value", "2"), ("Unit", "V") });

            yield return ("开关电源模块PS-24，输入AC220V，输出24V/2A，效率≥85%",
                new List<(string, string)> { ("ProductName", "开关电源模块PS-24"), ("Spec", "输入"), ("Value", "AC220"), ("Unit", "V"), ("Spec", "输出"), ("Value", "24"), ("Unit", "V"), ("Value", "2"), ("Unit", "A"), ("Tolerance", "≥85%") });

            yield return ("传感器PT100，测温范围-200~850℃，精度±0.15℃",
                new List<(string, string)> { ("ProductName", "传感器PT100"), ("Spec", "测温范围"), ("Value", "-200~850"), ("Unit", "℃"), ("Tolerance", "±0.15℃") });

            yield return ("继电器JQC-3F，线圈电压12V，触点容量10A 250VAC",
                new List<(string, string)> { ("ProductName", "继电器JQC-3F"), ("Spec", "线圈电压"), ("Value", "12"), ("Unit", "V"), ("Spec", "触点容量"), ("Value", "10"), ("Unit", "A"), ("Value", "250"), ("Unit", "VAC") });

            yield return ("连接器CON-20P，针数20，间距2.54mm，电流3A",
                new List<(string, string)> { ("ProductName", "连接器CON-20P"), ("Spec", "针数"), ("Value", "20"), ("Spec", "间距"), ("Value", "2.54"), ("Unit", "mm"), ("Spec", "电流"), ("Value", "3"), ("Unit", "A") });

            yield return ("保险丝F1A，额定电流1A，断流能力35A 250V",
                new List<(string, string)> { ("ProductName", "保险丝F1A"), ("Spec", "额定电流"), ("Value", "1"), ("Unit", "A"), ("Spec", "断流能力"), ("Value", "35"), ("Unit", "A"), ("Value", "250"), ("Unit", "V") });

            yield return ("晶振Y-12M，频率12MHz，负载电容20pF，精度±50ppm",
                new List<(string, string)> { ("ProductName", "晶振Y-12M"), ("Spec", "频率"), ("Value", "12"), ("Unit", "MHz"), ("Spec", "负载电容"), ("Value", "20"), ("Unit", "pF"), ("Tolerance", "±50ppm") });

            yield return ("滤波器EMI-01，插入损耗40dB@1MHz，额定电流3A",
                new List<(string, string)> { ("ProductName", "滤波器EMI-01"), ("Spec", "插入损耗"), ("Value", "40"), ("Unit", "dB"), ("Spec", "额定电流"), ("Value", "3"), ("Unit", "A") });

            yield return ("变压器T-220/12，初级220V，次级12V 1A，功率12VA",
                new List<(string, string)> { ("ProductName", "变压器T-220/12"), ("Spec", "初级"), ("Value", "220"), ("Unit", "V"), ("Spec", "次级"), ("Value", "12"), ("Unit", "V"), ("Value", "1"), ("Unit", "A"), ("Spec", "功率"), ("Value", "12"), ("Unit", "VA") });

            yield return ("电机M-24V，额定电压24V，空载转速3000rpm，扭矩0.5N·m",
                new List<(string, string)> { ("ProductName", "电机M-24V"), ("Spec", "额定电压"), ("Value", "24"), ("Unit", "V"), ("Spec", "空载转速"), ("Value", "3000"), ("Unit", "rpm"), ("Spec", "扭矩"), ("Value", "0.5"), ("Unit", "N·m") });

            yield return ("显示屏LCD-1602，5V供电，2行16字符，背光可选",
                new List<(string, string)> { ("ProductName", "显示屏LCD-1602"), ("Value", "5"), ("Unit", "V"), ("Spec", "供电"), ("Value", "2"), ("Spec", "行"), ("Value", "16"), ("Spec", "字符") });

            yield return ("电池组BAT-12V7AH，标称电压12V，容量7Ah，铅酸",
                new List<(string, string)> { ("ProductName", "电池组BAT-12V7AH"), ("Spec", "标称电压"), ("Value", "12"), ("Unit", "V"), ("Spec", "容量"), ("Value", "7"), ("Unit", "Ah"), ("Material", "铅酸") });

            yield return ("线缆AWG22，截面积0.33mm²，额定电流3A，耐压300V",
                new List<(string, string)> { ("ProductName", "线缆AWG22"), ("Spec", "截面积"), ("Value", "0.33"), ("Unit", "mm²"), ("Spec", "额定电流"), ("Value", "3"), ("Unit", "A"), ("Spec", "耐压"), ("Value", "300"), ("Unit", "V") });

            yield return ("热缩管Φ6，内径6mm，收缩比2:1，耐温125℃",
                new List<(string, string)> { ("ProductName", "热缩管Φ6"), ("Spec", "内径"), ("Value", "6"), ("Unit", "mm"), ("Spec", "收缩比"), ("Value", "2:1"), ("Spec", "耐温"), ("Value", "125"), ("Unit", "℃") });

            yield return ("端子TB-2P，间距5.08mm，额定10A，阻燃等级V0",
                new List<(string, string)> { ("ProductName", "端子TB-2P"), ("Spec", "间距"), ("Value", "5.08"), ("Unit", "mm"), ("Spec", "额定"), ("Value", "10"), ("Unit", "A"), ("Spec", "阻燃等级"), ("Value", "V0") });

            yield return ("散热器HS-25，热阻0.5℃/W，尺寸25×25×10mm",
                new List<(string, string)> { ("ProductName", "散热器HS-25"), ("Spec", "热阻"), ("Value", "0.5"), ("Unit", "℃/W"), ("Spec", "尺寸"), ("Value", "25×25×10"), ("Unit", "mm") });

            yield return ("外壳CASE-100，尺寸100×60×30mm，材质铝合金",
                new List<(string, string)> { ("ProductName", "外壳CASE-100"), ("Spec", "尺寸"), ("Value", "100×60×30"), ("Unit", "mm"), ("Material", "铝合金") });

            yield return ("PCB板厚1.6mm，铜厚1oz，阻焊绿色，字符白色",
                new List<(string, string)> { ("Spec", "板厚"), ("Value", "1.6"), ("Unit", "mm"), ("Spec", "铜厚"), ("Value", "1"), ("Unit", "oz"), ("Spec", "阻焊"), ("Value", "绿色"), ("Spec", "字符"), ("Value", "白色") });

            yield return ("焊锡Sn63Pb37，熔点183℃，线径0.8mm，含助焊剂",
                new List<(string, string)> { ("ProductName", "焊锡Sn63Pb37"), ("Spec", "熔点"), ("Value", "183"), ("Unit", "℃"), ("Spec", "线径"), ("Value", "0.8"), ("Unit", "mm"), ("Material", "助焊剂") });

            yield return ("绝缘胶带3M-1298，宽度19mm，耐压600V，厚度0.13mm",
                new List<(string, string)> { ("ProductName", "绝缘胶带3M-1298"), ("Spec", "宽度"), ("Value", "19"), ("Unit", "mm"), ("Spec", "耐压"), ("Value", "600"), ("Unit", "V"), ("Spec", "厚度"), ("Value", "0.13"), ("Unit", "mm") });

            yield return ("扎带2.5×100，宽度2.5mm，长度100mm，尼龙66",
                new List<(string, string)> { ("ProductName", "扎带2.5×100"), ("Spec", "宽度"), ("Value", "2.5"), ("Unit", "mm"), ("Spec", "长度"), ("Value", "100"), ("Unit", "mm"), ("Material", "尼龙66") });

            yield return ("标签纸L-50×30，尺寸50×30mm，材质聚酯，耐温-40~150℃",
                new List<(string, string)> { ("ProductName", "标签纸L-50×30"), ("Spec", "尺寸"), ("Value", "50×30"), ("Unit", "mm"), ("Material", "聚酯"), ("Spec", "耐温"), ("Value", "-40~150"), ("Unit", "℃") });

            yield return ("电阻R-1K，阻值1kΩ，功率0.25W，精度±5%",
                new List<(string, string)> { ("ProductName", "电阻R-1K"), ("Spec", "阻值"), ("Value", "1"), ("Unit", "kΩ"), ("Spec", "功率"), ("Value", "0.25"), ("Unit", "W"), ("Tolerance", "±5%") });

            yield return ("电解电容EC-470uF，容量470μF，耐压25V，寿命2000h",
                new List<(string, string)> { ("ProductName", "电解电容EC-470uF"), ("Spec", "容量"), ("Value", "470"), ("Unit", "μF"), ("Spec", "耐压"), ("Value", "25"), ("Unit", "V"), ("Spec", "寿命"), ("Value", "2000"), ("Unit", "h") });
        }
    }
}
