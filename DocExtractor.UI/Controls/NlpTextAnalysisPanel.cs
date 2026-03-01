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
    /// 文本分析页面
    /// 用户输入文本 → 模型预测高亮 → 人工校正 → 提交到训练集
    /// </summary>
    internal class NlpTextAnalysisPanel : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private NlpScenario _scenario;
        private Dictionary<string, Color> _entityColors = new Dictionary<string, Color>();

        // 当前预测的实体列表（用户可编辑）
        private List<ActiveEntityAnnotation> _currentEntities = new List<ActiveEntityAnnotation>();
        private string _currentText = string.Empty;
        private float _currentConfidence;

        public event Action? AnnotationSubmitted;

        // ── 控件字段 ─────────────────────────────────────────────────────────
        private RichTextBox _inputBox = null!;
        private Button _extractBtn = null!;
        private Button _clearBtn = null!;
        private RichTextBox _resultBox = null!;
        private DataGridView _entityGrid = null!;
        private Button _addEntityBtn = null!;
        private Button _deleteEntityBtn = null!;
        private Button _submitBtn = null!;
        private Label _confidenceLabel = null!;
        private Label _hintLabel = null!;
        private Panel _legendPanel = null!;

        public NlpTextAnalysisPanel(ActiveLearningEngine engine, NlpScenario scenario)
        {
            _engine   = engine;
            _scenario = scenario;
            InitializeLayout();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario      = scenario;
            _entityColors  = ScenarioManager.GetEntityColors(scenario);
            RebuildLegend();
            ClearAll();
        }

        // ── 布局 ──────────────────────────────────────────────────────────────

        private void InitializeLayout()
        {
            this.Dock    = DockStyle.Fill;
            this.Padding = new Padding(8);
            this.Font    = new Font("微软雅黑", 9F);

            _entityColors = ScenarioManager.GetEntityColors(_scenario);

            // 主分割：上（输入+结果）下（实体列表）
            var mainSplit = new SplitContainer
            {
                Dock         = DockStyle.Fill,
                Orientation  = Orientation.Horizontal,
                SplitterDistance = 300,
                Panel1MinSize = 180,
                Panel2MinSize = 160
            };

            // ── 上半：左（输入）右（结果）
            var topSplit = new SplitContainer
            {
                Dock             = DockStyle.Fill,
                Orientation      = Orientation.Vertical,
                SplitterDistance = 450
            };

            topSplit.Panel1.Controls.Add(BuildInputSection());
            topSplit.Panel2.Controls.Add(BuildResultSection());
            mainSplit.Panel1.Controls.Add(topSplit);

            // ── 下半：实体列表编辑
            mainSplit.Panel2.Controls.Add(BuildEntitySection());

            this.Controls.Add(mainSplit);
        }

        private Panel BuildInputSection()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 4, 0) };

            var title = new Label
            {
                Text      = "输入文本（支持粘贴整段文章）",
                Dock      = DockStyle.Top,
                Height    = 24,
                Font      = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            _inputBox = new RichTextBox
            {
                Dock         = DockStyle.Fill,
                Font         = new Font("微软雅黑", 10F),
                ScrollBars   = RichTextBoxScrollBars.Vertical,
                BorderStyle  = BorderStyle.FixedSingle,
                AcceptsTab   = true
            };
            _inputBox.Text = "请在此处输入或粘贴文本，支持单句、段落或整篇文章...";
            _inputBox.ForeColor = Color.Gray;
            _inputBox.GotFocus += (s, e) =>
            {
                if (_inputBox.Text == "请在此处输入或粘贴文本，支持单句、段落或整篇文章...")
                {
                    _inputBox.Text = "";
                    _inputBox.ForeColor = Color.Black;
                }
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Bottom,
                Height        = 38,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 4, 0, 0)
            };

            _extractBtn = new Button
            {
                Text      = "提取实体",
                Width     = 90,
                Height    = 30,
                BackColor = Color.FromArgb(24, 144, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 9F)
            };
            _extractBtn.FlatAppearance.BorderSize = 0;
            _extractBtn.Click += OnExtract;

            _clearBtn = new Button
            {
                Text      = "清空",
                Width     = 60,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 9F)
            };
            _clearBtn.Click += (s, e) => ClearAll();

            _confidenceLabel = new Label
            {
                Text      = "",
                Width     = 200,
                Height    = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(100, 100, 100)
            };

            btnPanel.Controls.AddRange(new Control[] { _extractBtn, _clearBtn, _confidenceLabel });

            panel.Controls.Add(_inputBox);
            panel.Controls.Add(btnPanel);
            panel.Controls.Add(title);
            return panel;
        }

        private Panel BuildResultSection()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 0, 0) };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 28 };

            var title = new Label
            {
                Text      = "提取结果（实体高亮显示）",
                Dock      = DockStyle.Left,
                Width     = 200,
                Height    = 24,
                Font      = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60)
            };

            _legendPanel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents  = false,
                AutoScroll    = false
            };

            topBar.Controls.Add(_legendPanel);
            topBar.Controls.Add(title);

            _resultBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                Font        = new Font("微软雅黑", 10.5F),
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = Color.FromArgb(250, 250, 250)
            };

            _hintLabel = new Label
            {
                Text      = "点击\"提取实体\"按钮，模型将在此处以彩色高亮显示识别到的实体",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.Gray,
                Font      = new Font("微软雅黑", 9.5F),
                Visible   = true
            };

            var resultHost = new Panel { Dock = DockStyle.Fill };
            resultHost.Controls.Add(_resultBox);
            resultHost.Controls.Add(_hintLabel);

            panel.Controls.Add(resultHost);
            panel.Controls.Add(topBar);

            RebuildLegend();
            return panel;
        }

        private Panel BuildEntitySection()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };

            var topBar = new Panel { Dock = DockStyle.Top, Height = 32 };

            var title = new Label
            {
                Text      = "实体列表（可添加 / 删除 / 修改类型，然后提交校正）",
                Dock      = DockStyle.Left,
                Width     = 400,
                Height    = 28,
                Font      = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(60, 60, 60),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _addEntityBtn = new Button
            {
                Text      = "+ 添加实体",
                Width     = 90,
                Height    = 28,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F),
                Dock      = DockStyle.Right
            };
            _addEntityBtn.Click += OnAddEntity;

            _deleteEntityBtn = new Button
            {
                Text      = "删除选中",
                Width     = 80,
                Height    = 28,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 8.5F),
                Dock      = DockStyle.Right
            };
            _deleteEntityBtn.Click += OnDeleteEntity;

            _submitBtn = new Button
            {
                Text      = "✓ 提交校正",
                Width     = 100,
                Height    = 28,
                BackColor = Color.FromArgb(82, 196, 26),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("微软雅黑", 9F, FontStyle.Bold),
                Dock      = DockStyle.Right
            };
            _submitBtn.FlatAppearance.BorderSize = 0;
            _submitBtn.Click += OnSubmitCorrection;

            topBar.Controls.Add(_submitBtn);
            topBar.Controls.Add(_deleteEntityBtn);
            topBar.Controls.Add(_addEntityBtn);
            topBar.Controls.Add(title);

            _entityGrid = new DataGridView
            {
                Dock                   = DockStyle.Fill,
                AllowUserToAddRows     = false,
                AllowUserToDeleteRows  = false,
                RowHeadersVisible      = false,
                SelectionMode          = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect            = false,
                AutoSizeColumnsMode    = DataGridViewAutoSizeColumnsMode.Fill,
                BorderStyle            = BorderStyle.None,
                Font                   = new Font("微软雅黑", 9F),
                GridColor              = Color.FromArgb(220, 220, 220),
                BackgroundColor        = Color.White,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight    = 30
            };
            BuildEntityGridColumns();

            panel.Controls.Add(_entityGrid);
            panel.Controls.Add(topBar);
            return panel;
        }

        private void BuildEntityGridColumns()
        {
            _entityGrid.Columns.Clear();

            var colText = new DataGridViewTextBoxColumn { Name = "Text", HeaderText = "实体文本", FillWeight = 30 };
            var colType = new DataGridViewComboBoxColumn
            {
                Name       = "Type",
                HeaderText = "实体类型",
                FillWeight = 20,
                FlatStyle  = FlatStyle.Flat
            };
            RefreshTypeComboItems(colType);

            var colStart  = new DataGridViewTextBoxColumn { Name = "Start",  HeaderText = "起始",   FillWeight = 10, ReadOnly = true };
            var colEnd    = new DataGridViewTextBoxColumn { Name = "End",    HeaderText = "结束",   FillWeight = 10, ReadOnly = true };
            var colConf   = new DataGridViewTextBoxColumn { Name = "Conf",   HeaderText = "置信度", FillWeight = 15, ReadOnly = true };
            var colManual = new DataGridViewCheckBoxColumn { Name = "Manual", HeaderText = "人工",   FillWeight = 8 };

            _entityGrid.Columns.AddRange(colText, colType, colStart, colEnd, colConf, colManual);

            _entityGrid.CellValueChanged += OnEntityGridCellChanged;
            _entityGrid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_entityGrid.IsCurrentCellDirty) _entityGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }

        private void RefreshTypeComboItems(DataGridViewComboBoxColumn col)
        {
            col.Items.Clear();
            foreach (var t in _scenario.EntityTypes) col.Items.Add(t);
        }

        private void RebuildLegend()
        {
            if (_legendPanel == null) return;
            _legendPanel.Controls.Clear();
            _entityColors = ScenarioManager.GetEntityColors(_scenario);
            foreach (var kv in _entityColors)
            {
                var chip = new Label
                {
                    Text      = kv.Key,
                    AutoSize  = false,
                    Width     = 70,
                    Height    = 20,
                    Margin    = new Padding(2),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = kv.Value,
                    Font      = new Font("微软雅黑", 8F)
                };
                _legendPanel.Controls.Add(chip);
            }
        }

        // ── 提取逻辑 ─────────────────────────────────────────────────────────

        private void OnExtract(object sender, EventArgs e)
        {
            string text = _inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || text == "请在此处输入或粘贴文本，支持单句、段落或整篇文章...")
            {
                MessageBox.Show("请先输入文本", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _extractBtn.Enabled = false;
            _extractBtn.Text    = "提取中...";

            try
            {
                var result = _engine.Predict(text, _scenario);
                _currentText       = text;
                _currentEntities   = new List<ActiveEntityAnnotation>(result.Entities);
                _currentConfidence = result.AvgConfidence;

                _hintLabel.Visible = false;
                RenderHighlightedResult();
                RefreshEntityGrid();

                string modelStatus = result.ModelLoaded ? "ML 模型" : "规则引擎";
                _confidenceLabel.Text = $"{modelStatus} | 实体 {result.Entities.Count} 个 | 置信度 {result.AvgConfidence:P0}";
                _confidenceLabel.ForeColor = result.AvgConfidence >= 0.9f ? Color.Green
                    : result.AvgConfidence >= 0.7f ? Color.DarkOrange : Color.Red;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"提取失败：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _extractBtn.Enabled = true;
                _extractBtn.Text    = "提取实体";
            }
        }

        private void RenderHighlightedResult()
        {
            _resultBox.Clear();
            if (string.IsNullOrEmpty(_currentText)) return;

            // 按起始位置排序，依次着色
            var sorted = _currentEntities.OrderBy(e => e.StartIndex).ToList();

            int cursor = 0;
            foreach (var entity in sorted)
            {
                int start = Math.Max(0, entity.StartIndex);
                int end   = Math.Min(_currentText.Length - 1, entity.EndIndex);

                if (start > cursor)
                {
                    AppendRtf(_currentText.Substring(cursor, start - cursor), Color.Black, Color.White);
                }

                Color bgColor = _entityColors.TryGetValue(entity.EntityType, out var c) ? c : Color.LightYellow;
                AppendRtf(entity.Text, Color.Black, bgColor);

                cursor = end + 1;
            }

            if (cursor < _currentText.Length)
                AppendRtf(_currentText.Substring(cursor), Color.Black, Color.White);
        }

        private void AppendRtf(string text, Color fg, Color bg)
        {
            int start = _resultBox.TextLength;
            _resultBox.AppendText(text);
            _resultBox.Select(start, text.Length);
            _resultBox.SelectionColor     = fg;
            _resultBox.SelectionBackColor = bg;
            _resultBox.SelectionLength    = 0;
        }

        // ── 实体列表 ─────────────────────────────────────────────────────────

        private void RefreshEntityGrid()
        {
            _entityGrid.Rows.Clear();

            foreach (var e in _currentEntities)
            {
                var row = new DataGridViewRow();
                row.CreateCells(_entityGrid,
                    e.Text,
                    e.EntityType,
                    e.StartIndex,
                    e.EndIndex,
                    $"{e.Confidence:P0}",
                    e.IsManual);
                _entityGrid.Rows.Add(row);
            }
        }

        private void OnEntityGridCellChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _currentEntities.Count) return;
            var row = _entityGrid.Rows[e.RowIndex];

            _currentEntities[e.RowIndex].Text       = row.Cells["Text"].Value?.ToString() ?? "";
            _currentEntities[e.RowIndex].EntityType = row.Cells["Type"].Value?.ToString() ?? "";
            _currentEntities[e.RowIndex].IsManual   = row.Cells["Manual"].Value is true;

            RenderHighlightedResult();
        }

        private void OnAddEntity(object sender, EventArgs e)
        {
            // 弹窗让用户指定实体文本和类型
            using var dlg = new AddEntityDialog(_scenario.EntityTypes, _currentText);
            if (dlg.ShowDialog() != DialogResult.OK) return;

            var entity = new ActiveEntityAnnotation
            {
                Text       = dlg.EntityText,
                EntityType = dlg.EntityType,
                StartIndex = dlg.StartIndex,
                EndIndex   = dlg.EndIndex,
                Confidence = 1f,
                IsManual   = true
            };
            _currentEntities.Add(entity);
            RefreshEntityGrid();
            RenderHighlightedResult();
        }

        private void OnDeleteEntity(object sender, EventArgs e)
        {
            if (_entityGrid.CurrentRow == null) return;
            int idx = _entityGrid.CurrentRow.Index;
            if (idx >= 0 && idx < _currentEntities.Count)
            {
                _currentEntities.RemoveAt(idx);
                RefreshEntityGrid();
                RenderHighlightedResult();
            }
        }

        private void OnSubmitCorrection(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_currentText))
            {
                MessageBox.Show("请先提取文本后再提交", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _engine.SubmitCorrection(
                _currentText,
                _currentEntities,
                _scenario.Id,
                _currentConfidence);

            MessageBox.Show(
                $"已提交 {_currentEntities.Count} 个实体标注到训练集！",
                "提交成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            AnnotationSubmitted?.Invoke();
            ClearAll();
        }

        private void ClearAll()
        {
            _inputBox.Text      = "请在此处输入或粘贴文本，支持单句、段落或整篇文章...";
            _inputBox.ForeColor = Color.Gray;
            _resultBox.Clear();
            _currentEntities.Clear();
            _currentText        = string.Empty;
            _currentConfidence  = 0f;
            _entityGrid.Rows.Clear();
            _confidenceLabel.Text = "";
            _hintLabel.Visible    = true;
        }
    }

    // ── 添加实体对话框 ────────────────────────────────────────────────────────

    internal class AddEntityDialog : Form
    {
        public string EntityText  { get; private set; } = string.Empty;
        public string EntityType  { get; private set; } = string.Empty;
        public int    StartIndex  { get; private set; }
        public int    EndIndex    { get; private set; }

        private TextBox    _textBox    = null!;
        private ComboBox   _typeCombo  = null!;
        private NumericUpDown _startSpin = null!;
        private NumericUpDown _endSpin   = null!;

        public AddEntityDialog(List<string> entityTypes, string sourceText)
        {
            Text            = "添加实体";
            Size            = new Size(400, 240);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = new Font("微软雅黑", 9F);

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(12),
                RowCount    = 5
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _textBox = new TextBox { Dock = DockStyle.Fill };

            _typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var t in entityTypes) _typeCombo.Items.Add(t);
            if (_typeCombo.Items.Count > 0) _typeCombo.SelectedIndex = 0;

            _startSpin = new NumericUpDown { Minimum = 0, Maximum = sourceText.Length, Dock = DockStyle.Fill };
            _endSpin   = new NumericUpDown { Minimum = 0, Maximum = sourceText.Length, Dock = DockStyle.Fill };

            var okBtn = new Button
            {
                Text      = "确定",
                DialogResult = DialogResult.OK,
                BackColor = Color.FromArgb(24, 144, 255),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            okBtn.FlatAppearance.BorderSize = 0;
            okBtn.Click += (s, e) =>
            {
                EntityText = _textBox.Text.Trim();
                EntityType = _typeCombo.SelectedItem?.ToString() ?? "";
                StartIndex = (int)_startSpin.Value;
                EndIndex   = (int)_endSpin.Value;
                if (string.IsNullOrEmpty(EntityText)) { EntityText = sourceText.Length > 0 ? sourceText.Substring(StartIndex, Math.Max(1, EndIndex - StartIndex + 1)) : ""; }
            };

            var cancelBtn = new Button { Text = "取消", DialogResult = DialogResult.Cancel, FlatStyle = FlatStyle.Flat };

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            layout.Controls.Add(new Label { Text = "实体文本", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_textBox);
            layout.Controls.Add(new Label { Text = "实体类型", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_typeCombo);
            layout.Controls.Add(new Label { Text = "起始位置", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_startSpin);
            layout.Controls.Add(new Label { Text = "结束位置", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }); layout.Controls.Add(_endSpin);
            layout.Controls.Add(new Label()); layout.Controls.Add(btnPanel);

            this.Controls.Add(layout);
            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;
        }
    }
}
