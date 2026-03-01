using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using Newtonsoft.Json;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// 场景驱动的多模式标注容器。
    /// </summary>
    internal class NlpUnifiedAnnotationPanel : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private NlpScenario _scenario;

        private ComboBox _modeCombo = null!;
        private Label _templateInfo = null!;
        private Label _trainHint = null!;
        private Button _quickTrainBtn = null!;
        private Button _openTrainingCenterBtn = null!;
        private Panel _editorHost = null!;

        private NlpTextAnalysisPanel? _spanPanel;
        private readonly Dictionary<AnnotationMode, StructuredModeEditor> _structuredEditors = new Dictionary<AnnotationMode, StructuredModeEditor>();

        public event Action? AnnotationSubmitted;
        public event Action? QuickTrainRequested;
        public event Action? OpenTrainingCenterRequested;

        public NlpUnifiedAnnotationPanel(ActiveLearningEngine engine, NlpScenario scenario)
        {
            _engine = engine;
            _scenario = scenario;
            InitializeLayout();
            BuildModeList();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario = scenario;
            _spanPanel?.SetScenario(scenario);
            foreach (var editor in _structuredEditors.Values)
                editor.SetScenario(scenario);
            BuildModeList();
        }

        private void InitializeLayout()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);

            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 46,
                Padding = new Padding(0, 6, 0, 4)
            };

            var modeLabel = new Label
            {
                Text = "标注模式：",
                Width = 70,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight
            };

            _modeCombo = new ComboBox
            {
                Width = 220,
                Height = 30,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _modeCombo.SelectedIndexChanged += (s, e) => SwitchMode();

            _templateInfo = new Label
            {
                Dock = DockStyle.Fill,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = NlpLabTheme.TextTertiary,
                Padding = new Padding(12, 0, 0, 0)
            };
            _trainHint = new Label
            {
                Width = 200,
                Height = 30,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = NlpLabTheme.TextTertiary
            };
            _quickTrainBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text = "快速训练",
                Width = 90,
                Height = 28
            });
            _quickTrainBtn.Click += (s, e) => QuickTrainRequested?.Invoke();
            _openTrainingCenterBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text = "训练中心",
                Width = 90,
                Height = 28
            });
            _openTrainingCenterBtn.Click += (s, e) => OpenTrainingCenterRequested?.Invoke();

            var leftFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Left,
                Width = 310,
                AutoSize = false,
                WrapContents = false,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0)
            };
            leftFlow.Controls.Add(modeLabel);
            leftFlow.Controls.Add(_modeCombo);

            var rightFlow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right,
                AutoSize = false,
                Width = 440,
                WrapContents = false,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(0)
            };
            rightFlow.Controls.Add(_trainHint);
            rightFlow.Controls.Add(_quickTrainBtn);
            rightFlow.Controls.Add(_openTrainingCenterBtn);

            _templateInfo.Dock = DockStyle.Fill;

            topBar.Controls.Add(_templateInfo);
            topBar.Controls.Add(rightFlow);
            topBar.Controls.Add(leftFlow);

            _editorHost = new Panel { Dock = DockStyle.Fill };

            Controls.Add(_editorHost);
            Controls.Add(topBar);
        }

        private void BuildModeList()
        {
            var modes = _scenario.EnabledModes != null && _scenario.EnabledModes.Count > 0
                ? _scenario.EnabledModes
                : new List<AnnotationMode> { AnnotationMode.SpanEntity };

            _modeCombo.Items.Clear();
            foreach (var mode in modes.Distinct())
                _modeCombo.Items.Add(new ModeItem(mode));

            _templateInfo.Text = $"模板配置：{GetTemplateName(_scenario.TemplateConfigJson)}";
            UpdateTrainingReadiness();

            if (_modeCombo.Items.Count > 0)
                _modeCombo.SelectedIndex = 0;
            else
                RenderEditor(null);
        }

        private void UpdateTrainingReadiness()
        {
            int verified = _engine.GetVerifiedCount(_scenario.Id);
            int min = _engine.MinSamplesForTraining;
            bool ready = verified >= min;
            _quickTrainBtn.Enabled = ready;
            _trainHint.Text = ready
                ? $"训练就绪（{verified}/{min}）"
                : $"还需样本（{verified}/{min}）";
            _trainHint.ForeColor = ready ? Color.FromArgb(82, 196, 26) : Color.DarkOrange;
        }

        private void SwitchMode()
        {
            var modeItem = _modeCombo.SelectedItem as ModeItem;
            if (modeItem == null)
            {
                RenderEditor(null);
                return;
            }

            if (modeItem.Mode == AnnotationMode.SpanEntity)
            {
                _spanPanel ??= CreateSpanPanel();
                RenderEditor(_spanPanel);
                return;
            }

            var editor = GetStructuredEditor(modeItem.Mode);
            RenderEditor(editor);
        }

        private NlpTextAnalysisPanel CreateSpanPanel()
        {
            var panel = new NlpTextAnalysisPanel(_engine, _scenario);
            panel.AnnotationSubmitted += () =>
            {
                UpdateTrainingReadiness();
                AnnotationSubmitted?.Invoke();
            };
            return panel;
        }

        private StructuredModeEditor GetStructuredEditor(AnnotationMode mode)
        {
            if (_structuredEditors.TryGetValue(mode, out var existing))
                return existing;

            var editor = new StructuredModeEditor(_engine, _scenario, mode);
            editor.AnnotationSubmitted += () =>
            {
                UpdateTrainingReadiness();
                AnnotationSubmitted?.Invoke();
            };
            _structuredEditors[mode] = editor;
            return editor;
        }

        private void RenderEditor(Control? editor)
        {
            _editorHost.Controls.Clear();
            if (editor == null) return;
            editor.Dock = DockStyle.Fill;
            _editorHost.Controls.Add(editor);
        }

        private static string GetTemplateName(string templateJson)
        {
            if (string.IsNullOrWhiteSpace(templateJson))
                return "默认模板";

            try
            {
                var template = JsonConvert.DeserializeObject<AnnotationTemplateDefinition>(templateJson);
                return string.IsNullOrWhiteSpace(template?.Name) ? "默认模板" : template.Name;
            }
            catch
            {
                return "默认模板";
            }
        }

        private sealed class ModeItem
        {
            public ModeItem(AnnotationMode mode) => Mode = mode;
            public AnnotationMode Mode { get; }
            public override string ToString() => AnnotationModeHelper.GetDisplayName(Mode);
        }
    }

    internal sealed class StructuredModeEditor : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private NlpScenario _scenario;
        private readonly AnnotationMode _mode;

        private TextBox _rawTextBox = null!;
        private DataGridView _grid = null!;
        private Label _hint = null!;
        private Button _submitBtn = null!;

        public event Action? AnnotationSubmitted;

        public StructuredModeEditor(ActiveLearningEngine engine, NlpScenario scenario, AnnotationMode mode)
        {
            _engine = engine;
            _scenario = scenario;
            _mode = mode;
            BuildLayout();
            BuildColumns();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario = scenario;
            _hint.Text = BuildHintText();
        }

        private void BuildLayout()
        {
            Dock = DockStyle.Fill;
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Padding = new Padding(0)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _rawTextBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical
            };

            _hint = new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = NlpLabTheme.TextTertiary,
                Font = NlpLabTheme.Small,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = BuildHintText()
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true
            };
            NlpLabTheme.StyleGrid(_grid);

            var submitBar = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 6, 0, 0) };
            _submitBtn = NlpLabTheme.MakeSuccess(new Button
            {
                Text = "提交结构化标注",
                Width = 130,
                Height = 30,
                Dock = DockStyle.Right
            });
            _submitBtn.Click += OnSubmit;
            submitBar.Controls.Add(_submitBtn);

            root.Controls.Add(_rawTextBox, 0, 0);
            root.Controls.Add(_hint, 0, 1);
            root.Controls.Add(_grid, 0, 2);
            root.Controls.Add(submitBar, 0, 3);
            Controls.Add(root);
        }

        private string BuildHintText()
        {
            return $"模式：{AnnotationModeHelper.GetDisplayName(_mode)}。请先录入原始文本，再填写结构化行并提交。";
        }

        private void BuildColumns()
        {
            _grid.Columns.Clear();
            foreach (var col in GetColumnsByMode(_mode))
            {
                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = col,
                    HeaderText = col,
                    FillWeight = 100
                });
            }
        }

        private static List<string> GetColumnsByMode(AnnotationMode mode)
        {
            return mode switch
            {
                AnnotationMode.KvSchema => new List<string> { "设备", "子系统", "参数", "取值", "单位", "备注" },
                AnnotationMode.EnumBitfield => new List<string> { "位段", "值", "语义", "备注" },
                AnnotationMode.Relation => new List<string> { "主体", "关系", "客体", "证据片段" },
                AnnotationMode.Sequence => new List<string> { "步骤", "事件", "前置条件", "结果状态" },
                _ => new List<string> { "字段", "值", "备注" }
            };
        }

        private void OnSubmit(object? sender, EventArgs e)
        {
            string rawText = _rawTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(rawText))
            {
                MessageBox.Show("请先输入原始文本。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var rows = CollectRows();
            if (rows.Count == 0)
            {
                MessageBox.Show("请至少填写一行结构化标注。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!ValidateRows(rows, out var error))
            {
                MessageBox.Show(error, "校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var payload = new
            {
                mode = _mode.ToString(),
                scenarioId = _scenario.Id,
                template = _scenario.TemplateConfigJson,
                rows
            };

            _engine.SubmitStructuredCorrection(
                rawText,
                _scenario.Id,
                _mode,
                JsonConvert.SerializeObject(payload));

            MessageBox.Show("结构化标注已提交。", "提交成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            AnnotationSubmitted?.Invoke();
        }

        private List<Dictionary<string, string>> CollectRows()
        {
            var result = new List<Dictionary<string, string>>();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                bool hasValue = false;
                foreach (DataGridViewColumn col in _grid.Columns)
                {
                    string value = row.Cells[col.Name].Value?.ToString()?.Trim() ?? string.Empty;
                    dict[col.Name] = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        hasValue = true;
                }
                if (hasValue)
                    result.Add(dict);
            }
            return result;
        }

        private bool ValidateRows(List<Dictionary<string, string>> rows, out string error)
        {
            error = string.Empty;
            if (_mode == AnnotationMode.Relation)
            {
                if (rows.Any(r => string.IsNullOrWhiteSpace(Get(r, "主体")) || string.IsNullOrWhiteSpace(Get(r, "关系")) || string.IsNullOrWhiteSpace(Get(r, "客体"))))
                {
                    error = "关系模式要求每行都填写主体/关系/客体。";
                    return false;
                }
            }
            else if (_mode == AnnotationMode.Sequence)
            {
                var stepValues = rows.Select(r => Get(r, "步骤")).ToList();
                if (stepValues.Any(string.IsNullOrWhiteSpace))
                {
                    error = "时序模式要求每行都填写步骤。";
                    return false;
                }
            }
            else if (_mode == AnnotationMode.KvSchema)
            {
                if (rows.Any(r => string.IsNullOrWhiteSpace(Get(r, "设备")) || string.IsNullOrWhiteSpace(Get(r, "参数"))))
                {
                    error = "结构化键值模式要求每行至少填写设备和参数。";
                    return false;
                }
            }
            else if (_mode == AnnotationMode.EnumBitfield)
            {
                if (rows.Any(r => string.IsNullOrWhiteSpace(Get(r, "位段")) || string.IsNullOrWhiteSpace(Get(r, "值"))))
                {
                    error = "枚举位段模式要求每行至少填写位段和值。";
                    return false;
                }
            }

            return true;
        }

        private static string Get(Dictionary<string, string> row, string key)
            => row.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }
}
