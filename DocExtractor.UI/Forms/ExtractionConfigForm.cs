using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using DocExtractor.Core.Models;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// 抽取配置编辑窗口：管理目标字段定义和拆分规则
    /// </summary>
    public class ExtractionConfigForm : Form
    {
        public ExtractionConfig Config { get; private set; }

        private DataGridView _fieldsGrid = null!;
        private DataGridView _splitGrid = null!;
        private NumericUpDown _headerRowsSpinner = null!;
        private ComboBox _columnMatchCombo = null!;
        private ComboBox _tableSelectionCombo = null!;

        public ExtractionConfigForm(ExtractionConfig config)
        {
            Config = CloneConfig(config);
            InitializeComponent();
            LoadConfig();
        }

        private void InitializeComponent()
        {
            Text = "抽取配置";
            Size = new Size(900, 700);
            StartPosition = FormStartPosition.CenterParent;

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // ── Tab 1：字段定义 ──────────────────────────────────────────────
            var fieldTab = new TabPage { Text = "字段定义" };
            _fieldsGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _fieldsGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "FieldName", HeaderText = "字段名（英文）", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "显示名（中文）", FillWeight = 15 },
                new DataGridViewComboBoxColumn
                {
                    Name = "DataType", HeaderText = "数据类型",
                    DataSource = Enum.GetNames(typeof(FieldDataType)),
                    FillWeight = 12
                },
                new DataGridViewCheckBoxColumn { Name = "IsRequired", HeaderText = "必填", FillWeight = 8 },
                new DataGridViewTextBoxColumn { Name = "Variants", HeaderText = "列名变体（逗号分隔）", FillWeight = 50 }
            });
            fieldTab.Controls.Add(_fieldsGrid);

            // ── Tab 2：拆分规则 ──────────────────────────────────────────────
            var splitTab = new TabPage { Text = "拆分规则" };
            _splitGrid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };
            _splitGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "RuleName", HeaderText = "规则名", FillWeight = 15 },
                new DataGridViewComboBoxColumn
                {
                    Name = "Type", HeaderText = "拆分类型",
                    DataSource = Enum.GetNames(typeof(SplitType)),
                    FillWeight = 20
                },
                new DataGridViewTextBoxColumn { Name = "TriggerColumn", HeaderText = "触发字段", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "Delimiters", HeaderText = "分隔符", FillWeight = 15 },
                new DataGridViewCheckBoxColumn { Name = "InheritParent", HeaderText = "继承父行", FillWeight = 10 },
                new DataGridViewTextBoxColumn { Name = "Priority", HeaderText = "优先级", FillWeight = 10 }
            });
            splitTab.Controls.Add(_splitGrid);

            // ── Tab 3：全局设置 ──────────────────────────────────────────────
            var settingsTab = new TabPage { Text = "全局设置" };
            var settingsLayout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2 };

            void AddSetting(string label, Control ctrl, int row)
            {
                settingsLayout.Controls.Add(new Label
                {
                    Text = label,
                    TextAlign = ContentAlignment.MiddleRight,
                    Dock = DockStyle.Fill
                }, 0, row);
                settingsLayout.Controls.Add(ctrl, 1, row);
            }

            _headerRowsSpinner = new NumericUpDown { Minimum = 1, Maximum = 5, Value = Config.HeaderRowCount, Width = 80 };
            _columnMatchCombo = new ComboBox
            {
                DataSource = Enum.GetNames(typeof(ColumnMatchMode)),
                SelectedItem = Config.ColumnMatch.ToString(),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };
            _tableSelectionCombo = new ComboBox
            {
                DataSource = Enum.GetNames(typeof(TableSelectionMode)),
                SelectedItem = Config.TableSelection.ToString(),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 200
            };

            AddSetting("表头行数：", _headerRowsSpinner, 0);
            AddSetting("列名匹配模式：", _columnMatchCombo, 1);
            AddSetting("表格选择策略：", _tableSelectionCombo, 2);

            settingsTab.Controls.Add(settingsLayout);

            tabs.TabPages.AddRange(new[] { fieldTab, splitTab, settingsTab });

            // ── 底部按钮 ─────────────────────────────────────────────────────
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8)
            };

            var cancelBtn = new Button { Text = "取消", Width = 80, Height = 32, DialogResult = DialogResult.Cancel };
            var okBtn = new Button
            {
                Text = "确定",
                Width = 80,
                Height = 32,
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(0, 120, 215),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okBtn.Click += OnOk;

            btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            Controls.Add(tabs);
            Controls.Add(btnPanel);
        }

        private void LoadConfig()
        {
            // 加载字段
            foreach (var f in Config.Fields)
            {
                _fieldsGrid.Rows.Add(
                    f.FieldName,
                    f.DisplayName,
                    f.DataType.ToString(),
                    f.IsRequired,
                    string.Join(",", f.KnownColumnVariants));
            }

            // 加载拆分规则
            foreach (var r in Config.SplitRules)
            {
                _splitGrid.Rows.Add(
                    r.RuleName,
                    r.Type.ToString(),
                    r.TriggerColumn,
                    string.Join(",", r.Delimiters),
                    r.InheritParentFields,
                    r.Priority.ToString());
            }
        }

        private void OnOk(object? sender, EventArgs e)
        {
            // 保存字段定义
            Config.Fields.Clear();
            foreach (DataGridViewRow row in _fieldsGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var fieldName = row.Cells["FieldName"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(fieldName)) continue;

                var f = new FieldDefinition
                {
                    FieldName = fieldName,
                    DisplayName = row.Cells["DisplayName"].Value?.ToString() ?? fieldName,
                    IsRequired = row.Cells["IsRequired"].Value is true
                };

                if (Enum.TryParse<FieldDataType>(row.Cells["DataType"].Value?.ToString(), out var dt))
                    f.DataType = dt;

                var variants = row.Cells["Variants"].Value?.ToString() ?? string.Empty;
                f.KnownColumnVariants = new List<string>(
                    variants.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

                Config.Fields.Add(f);
            }

            // 保存拆分规则
            Config.SplitRules.Clear();
            foreach (DataGridViewRow row in _splitGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var ruleName = row.Cells["RuleName"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(ruleName)) continue;

                var r = new SplitRule
                {
                    RuleName = ruleName,
                    TriggerColumn = row.Cells["TriggerColumn"].Value?.ToString() ?? string.Empty,
                    InheritParentFields = row.Cells["InheritParent"].Value is true
                };

                if (Enum.TryParse<SplitType>(row.Cells["Type"].Value?.ToString(), out var st))
                    r.Type = st;

                var delimiters = row.Cells["Delimiters"].Value?.ToString() ?? "/;、";
                r.Delimiters = new List<string>(
                    delimiters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

                if (int.TryParse(row.Cells["Priority"].Value?.ToString(), out int pri))
                    r.Priority = pri;

                Config.SplitRules.Add(r);
            }

            // 全局设置
            Config.HeaderRowCount = (int)_headerRowsSpinner.Value;
            if (Enum.TryParse<ColumnMatchMode>(_columnMatchCombo.SelectedItem?.ToString(), out var cm))
                Config.ColumnMatch = cm;
            if (Enum.TryParse<TableSelectionMode>(_tableSelectionCombo.SelectedItem?.ToString(), out var ts))
                Config.TableSelection = ts;
        }

        private static ExtractionConfig CloneConfig(ExtractionConfig src)
        {
            return new ExtractionConfig
            {
                ConfigName = src.ConfigName,
                Fields = new List<FieldDefinition>(src.Fields),
                SplitRules = new List<SplitRule>(src.SplitRules),
                HeaderRowCount = src.HeaderRowCount,
                ColumnMatch = src.ColumnMatch,
                TableSelection = src.TableSelection,
                TableIndices = new List<int>(src.TableIndices),
                TableKeywords = new List<string>(src.TableKeywords),
                SkipEmptyRows = src.SkipEmptyRows,
                TargetSheets = src.TargetSheets != null ? new List<string>(src.TargetSheets) : null
            };
        }
    }
}
