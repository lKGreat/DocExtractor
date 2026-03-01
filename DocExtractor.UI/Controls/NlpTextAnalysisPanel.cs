using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;

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

        private List<ActiveEntityAnnotation> _currentEntities = new List<ActiveEntityAnnotation>();
        private string _currentText = string.Empty;
        private float _currentConfidence;
        private bool _isPlaceholder = true;

        public event Action? AnnotationSubmitted;

        // ── 控件字段 ─────────────────────────────────────────────────────────
        private RichTextBox _inputBox = null!;
        private Button _extractBtn = null!;
        private Button _clearBtn = null!;
        private Button _tagSelectionBtn = null!;
        private RichTextBox _resultBox = null!;
        private DataGridView _entityGrid = null!;
        private Button _addEntityBtn = null!;
        private Button _deleteEntityBtn = null!;
        private Button _submitBtn = null!;
        private Label _confidenceLabel = null!;
        private Label _hintLabel = null!;
        private FlowLayoutPanel _legendPanel = null!;
        private ContextMenuStrip _tagContextMenu = null!;

        public NlpTextAnalysisPanel(ActiveLearningEngine engine, NlpScenario scenario)
        {
            _engine   = engine;
            _scenario = scenario;
            InitializeLayout();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario     = scenario;
            _entityColors = ScenarioManager.GetEntityColors(scenario);
            RebuildLegend();
            RebuildTagContextMenu();
            ClearAll();
        }

        // ── 布局 ──────────────────────────────────────────────────────────────

        private void InitializeLayout()
        {
            this.Dock    = DockStyle.Fill;
            this.Padding = new Padding(8);
            this.Font    = NlpLabTheme.Body;

            _entityColors = ScenarioManager.GetEntityColors(_scenario);

            var mainSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Height      = 800
            };
            NlpLabTheme.SetSplitterDistanceDeferred(mainSplit, 0.6, panel1Min: 180, panel2Min: 140);

            var topSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Width       = 1200
            };
            NlpLabTheme.SetSplitterDistanceDeferred(topSplit, 0.45, panel1Min: 240, panel2Min: 300);

            topSplit.Panel1.Controls.Add(BuildInputSection());
            topSplit.Panel2.Controls.Add(BuildResultSection());
            mainSplit.Panel1.Controls.Add(topSplit);

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
                Height    = 26,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _inputBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                Font        = NlpLabTheme.TextInput,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                AcceptsTab  = true
            };
            SetPlaceholder();
            _inputBox.GotFocus += (s, e) =>
            {
                if (_isPlaceholder)
                {
                    _inputBox.Text      = "";
                    _inputBox.ForeColor = NlpLabTheme.TextPrimary;
                    _isPlaceholder      = false;
                }
            };
            _inputBox.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_inputBox.Text))
                    SetPlaceholder();
            };

            var btnPanel = new FlowLayoutPanel
            {
                Dock          = DockStyle.Bottom,
                Height        = 38,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 4, 0, 0)
            };

            _extractBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text   = "提取实体",
                Width  = 90,
                Height = 30
            });
            _extractBtn.Click += OnExtract;

            _tagSelectionBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "标记选中文本",
                Width  = 100,
                Height = 30
            });
            _tagSelectionBtn.Click += OnTagSelection;

            _clearBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text   = "清空",
                Width  = 60,
                Height = 30
            });
            _clearBtn.Click += (s, e) => ClearAll();

            _confidenceLabel = new Label
            {
                Text      = "",
                Width     = 250,
                Height    = 30,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary
            };

            btnPanel.Controls.AddRange(new Control[] { _extractBtn, _tagSelectionBtn, _clearBtn, _confidenceLabel });

            RebuildTagContextMenu();

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
                Height    = 26,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
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
                Font        = NlpLabTheme.TextResult,
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor   = NlpLabTheme.BgInput
            };

            _hintLabel = new Label
            {
                Text      = "点击\"提取实体\"按钮，模型将在此处以彩色高亮显示识别到的实体",
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = NlpLabTheme.TextTertiary,
                Font      = NlpLabTheme.Body,
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

            var topBar = new Panel { Dock = DockStyle.Top, Height = 34 };

            var title = new Label
            {
                Text      = "实体列表（可添加 / 删除 / 修改类型，然后提交校正）",
                Dock      = DockStyle.Left,
                Width     = 400,
                Height    = 30,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _addEntityBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "+ 添加实体",
                Width  = 90,
                Height = 28,
                Dock   = DockStyle.Right
            });
            _addEntityBtn.Click += OnAddEntity;

            _deleteEntityBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text   = "删除选中",
                Width  = 80,
                Height = 28,
                Dock   = DockStyle.Right
            });
            _deleteEntityBtn.Click += OnDeleteEntity;

            _submitBtn = NlpLabTheme.MakeSuccess(new Button
            {
                Text   = "✓ 提交校正",
                Width  = 100,
                Height = 28,
                Dock   = DockStyle.Right
            });
            _submitBtn.Click += OnSubmitCorrection;

            topBar.Controls.Add(_submitBtn);
            topBar.Controls.Add(_deleteEntityBtn);
            topBar.Controls.Add(_addEntityBtn);
            topBar.Controls.Add(title);

            _entityGrid = new DataGridView { Dock = DockStyle.Fill };
            NlpLabTheme.StyleGrid(_entityGrid);
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
                    AutoSize  = true,
                    Height    = 20,
                    Margin    = new Padding(2),
                    Padding   = new Padding(6, 0, 6, 0),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = kv.Value,
                    Font      = NlpLabTheme.Small
                };
                _legendPanel.Controls.Add(chip);
            }
        }

        private void RebuildTagContextMenu()
        {
            _tagContextMenu = new ContextMenuStrip { Font = NlpLabTheme.Body };
            _tagContextMenu.Items.Clear();
            foreach (var kv in _entityColors)
            {
                var type = kv.Key;
                var color = kv.Value;
                var item = new ToolStripMenuItem(type) { BackColor = color };
                item.Click += (s, e) => TagSelectedText(type);
                _tagContextMenu.Items.Add(item);
            }
        }

        // ── 文本选择标注 ─────────────────────────────────────────────────────

        private void OnTagSelection(object sender, EventArgs e)
        {
            if (_inputBox.SelectionLength == 0)
            {
                MessageBox.Show("请先在输入框中选中要标记的文本", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _tagContextMenu.Show(_tagSelectionBtn, new Point(0, _tagSelectionBtn.Height));
        }

        private void TagSelectedText(string entityType)
        {
            string text = _inputBox.SelectedText;
            int start   = _inputBox.SelectionStart;
            int end     = start + text.Length - 1;

            if (string.IsNullOrWhiteSpace(text)) return;

            var entity = new ActiveEntityAnnotation
            {
                Text       = text,
                EntityType = entityType,
                StartIndex = start,
                EndIndex   = end,
                Confidence = 1f,
                IsManual   = true
            };

            _currentEntities.Add(entity);
            _currentText = _inputBox.Text;
            _hintLabel.Visible = false;
            RefreshEntityGrid();
            RenderHighlightedResult();
        }

        // ── 提取逻辑 ─────────────────────────────────────────────────────────

        private async void OnExtract(object sender, EventArgs e)
        {
            string text = _inputBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(text) || _isPlaceholder)
            {
                MessageBox.Show("请先输入文本", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _extractBtn.Enabled = false;
            _extractBtn.Text    = "提取中...";

            try
            {
                var scenario = _scenario;
                var result = await Task.Run(() => _engine.Predict(text, scenario));

                _currentText       = text;
                _currentEntities   = new List<ActiveEntityAnnotation>(result.Entities);
                _currentConfidence = result.AvgConfidence;

                _hintLabel.Visible = false;
                RenderHighlightedResult();
                RefreshEntityGrid();

                string modelStatus = result.ModelLoaded ? "ML 模型" : "规则引擎";
                _confidenceLabel.Text = $"{modelStatus} | 实体 {result.Entities.Count} 个 | 置信度 {result.AvgConfidence:P0}";
                _confidenceLabel.ForeColor = result.AvgConfidence >= 0.9f
                    ? Color.FromArgb(82, 196, 26)
                    : result.AvgConfidence >= 0.7f
                        ? Color.DarkOrange
                        : NlpLabTheme.Danger;
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

            var sorted = _currentEntities.OrderBy(e => e.StartIndex).ToList();

            int cursor = 0;
            foreach (var entity in sorted)
            {
                int start = Math.Max(0, entity.StartIndex);
                int end   = Math.Min(_currentText.Length - 1, entity.EndIndex);

                if (start > cursor)
                    AppendRtf(_currentText.Substring(cursor, start - cursor), NlpLabTheme.TextPrimary, Color.White);

                Color bgColor = _entityColors.TryGetValue(entity.EntityType, out var c) ? c : Color.LightYellow;
                AppendRtf(entity.Text, NlpLabTheme.TextPrimary, bgColor);

                cursor = end + 1;
            }

            if (cursor < _currentText.Length)
                AppendRtf(_currentText.Substring(cursor), NlpLabTheme.TextPrimary, Color.White);
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
            int selStart  = _inputBox.SelectionStart;
            int selLength = _inputBox.SelectionLength;
            string selText = _inputBox.SelectedText;

            using var dlg = new AddEntityDialog(_scenario.EntityTypes, _currentText, selText, selStart, selLength);
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
            _currentText = _inputBox.Text;
            _hintLabel.Visible = false;
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

        // ── Helpers ─────────────────────────────────────────────────────────

        private void SetPlaceholder()
        {
            _isPlaceholder      = true;
            _inputBox.Text      = "请在此处输入或粘贴文本，支持单句、段落或整篇文章...";
            _inputBox.ForeColor = NlpLabTheme.TextTertiary;
        }

        private void ClearAll()
        {
            SetPlaceholder();
            _resultBox.Clear();
            _currentEntities.Clear();
            _currentText       = string.Empty;
            _currentConfidence = 0f;
            _entityGrid.Rows.Clear();
            _confidenceLabel.Text = "";
            _hintLabel.Visible    = true;
        }
    }

    // ── 添加实体对话框（已改造：优先使用选区，无需手动输入索引）──────────────

    internal class AddEntityDialog : Form
    {
        public string EntityText  { get; private set; } = string.Empty;
        public string EntityType  { get; private set; } = string.Empty;
        public int    StartIndex  { get; private set; }
        public int    EndIndex    { get; private set; }

        private TextBox  _textBox   = null!;
        private ComboBox _typeCombo = null!;

        public AddEntityDialog(
            List<string> entityTypes,
            string sourceText,
            string selectedText,
            int selectionStart,
            int selectionLength)
        {
            Text            = "添加实体";
            Size            = new Size(420, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            Font            = NlpLabTheme.Body;

            bool hasSelection = !string.IsNullOrWhiteSpace(selectedText) && selectionLength > 0;

            var layout = new TableLayoutPanel
            {
                Dock        = DockStyle.Fill,
                ColumnCount = 2,
                Padding     = new Padding(14),
                RowCount    = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            _textBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Text = hasSelection ? selectedText : "",
                ReadOnly = hasSelection
            };

            _typeCombo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            foreach (var t in entityTypes) _typeCombo.Items.Add(t);
            if (_typeCombo.Items.Count > 0) _typeCombo.SelectedIndex = 0;

            var hint = new Label
            {
                Text      = hasSelection
                    ? $"已从输入框选中文本（位置 {selectionStart}–{selectionStart + selectionLength - 1}）"
                    : "提示：先在输入框中选中文本再点击添加，可自动填充",
                Dock      = DockStyle.Fill,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var okBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text         = "确定",
                DialogResult = DialogResult.OK,
                Width        = 80,
                Height       = 32
            });
            okBtn.Click += (s, e) =>
            {
                EntityText = _textBox.Text.Trim();
                EntityType = _typeCombo.SelectedItem?.ToString() ?? "";
                if (hasSelection)
                {
                    StartIndex = selectionStart;
                    EndIndex   = selectionStart + selectionLength - 1;
                    if (string.IsNullOrEmpty(EntityText))
                        EntityText = selectedText;
                }
                else
                {
                    int found = (sourceText ?? "").IndexOf(EntityText, StringComparison.Ordinal);
                    StartIndex = found >= 0 ? found : 0;
                    EndIndex   = StartIndex + Math.Max(1, EntityText.Length) - 1;
                }

                if (string.IsNullOrEmpty(EntityText) || string.IsNullOrEmpty(EntityType))
                {
                    MessageBox.Show("实体文本和类型不能为空", "提示");
                    DialogResult = DialogResult.None;
                }
            };

            var cancelBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text         = "取消",
                DialogResult = DialogResult.Cancel,
                Width        = 80,
                Height       = 32
            });

            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { cancelBtn, okBtn });

            layout.Controls.Add(new Label { Text = "实体文本", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill });
            layout.Controls.Add(_textBox);
            layout.Controls.Add(new Label { Text = "实体类型", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill });
            layout.Controls.Add(_typeCombo);
            layout.Controls.Add(new Label());
            layout.Controls.Add(hint);
            layout.Controls.Add(new Label());
            layout.Controls.Add(btnPanel);

            this.Controls.Add(layout);
            this.AcceptButton = okBtn;
            this.CancelButton = cancelBtn;
        }
    }
}
