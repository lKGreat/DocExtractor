using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using DocExtractor.ML.Training;
using Newtonsoft.Json;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// 主动学习页面
    /// 展示模型最不确定的文本队列 → 批量标注 → 一键训练 → 质量门控
    /// </summary>
    internal class NlpActiveLearningPanel : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private NlpScenario _scenario;
        private CancellationTokenSource? _trainCts;

        public event Action? TrainingCompleted;
        public event Action<LearningSessionResult>? TrainingCompletedDetailed;

        // ── 控件字段 ─────────────────────────────────────────────────────────
        private Label _statsLabel          = null!;
        private Label _qualityLabel        = null!;
        private TabControl _leftTabs       = null!;
        private DataGridView _queueGrid    = null!;
        private DataGridView _verifiedGrid = null!;
        private Button _refreshQueueBtn    = null!;
        private Button _deleteVerifiedBtn  = null!;
        private RichTextBox _annotateBox   = null!;
        private DataGridView _editGrid     = null!;
        private Button _confirmAnnotateBtn = null!;
        private Button _skipBtn            = null!;
        private Button _trainBtn           = null!;
        private Button _cancelTrainBtn     = null!;
        private ProgressBar _trainProgress = null!;
        private RichTextBox _trainLog      = null!;
        private Label _trainStatusLabel    = null!;
        private ComboBox _presetCombo      = null!;

        private List<NlpUncertainEntry> _queue = new List<NlpUncertainEntry>();
        private NlpUncertainEntry? _currentEntry;
        private List<NlpAnnotatedText> _verifiedSamples = new List<NlpAnnotatedText>();
        private NlpAnnotatedText? _currentVerified;
        private List<ActiveEntityAnnotation> _currentAnnotations = new List<ActiveEntityAnnotation>();

        public NlpActiveLearningPanel(ActiveLearningEngine engine, NlpScenario scenario)
        {
            _engine   = engine;
            _scenario = scenario;
            InitializeLayout();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario = scenario;
            RefreshStats();
            LoadQueue();
            LoadVerifiedSamples();
        }

        public void OnActivated()
        {
            RefreshStats();
            LoadQueue();
            LoadVerifiedSamples();
        }

        public bool CanStartTraining => _engine.GetVerifiedCount(_scenario.Id) >= _engine.MinSamplesForTraining;

        public string TrainingReadinessMessage
        {
            get
            {
                int verified = _engine.GetVerifiedCount(_scenario.Id);
                if (verified >= _engine.MinSamplesForTraining)
                    return $"可训练：当前已标注 {verified} 条。";
                return $"样本不足：还需 {_engine.MinSamplesForTraining - verified} 条。";
            }
        }

        public void TriggerTraining(int presetIndex = 1)
        {
            if (_trainBtn.Enabled == false) return;
            if (presetIndex < 0 || presetIndex >= _presetCombo.Items.Count) presetIndex = 1;
            _presetCombo.SelectedIndex = presetIndex;
            OnTrain(this, EventArgs.Empty);
        }

        // ── 布局 ──────────────────────────────────────────────────────────────

        private void InitializeLayout()
        {
            this.Dock    = DockStyle.Fill;
            this.Padding = new Padding(8);
            this.Font    = NlpLabTheme.Body;

            var mainSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Width       = 1200
            };
            NlpLabTheme.SetSplitterDistanceDeferred(mainSplit, 0.32, panel1Min: 250, panel2Min: 400);

            mainSplit.Panel1.Controls.Add(BuildLeftPanel());
            mainSplit.Panel2.Controls.Add(BuildRightPanel());

            this.Controls.Add(mainSplit);
        }

        private Panel BuildLeftPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 4, 0) };

            // ── 统计栏 ──
            var statsBar = new Panel { Dock = DockStyle.Top, Height = 56, Padding = new Padding(0, 4, 0, 4) };

            _statsLabel = new Label
            {
                Text      = "已标注 0 条 | 待审核 0 条",
                Dock      = DockStyle.Top,
                Height    = 24,
                Font      = NlpLabTheme.Body,
                ForeColor = NlpLabTheme.TextPrimary
            };
            _qualityLabel = new Label
            {
                Text      = "当前 F1: — | Precision: — | Recall: —",
                Dock      = DockStyle.Top,
                Height    = 24,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary
            };
            statsBar.Controls.Add(_qualityLabel);
            statsBar.Controls.Add(_statsLabel);

            // ── TabControl：不确定性队列 + 已标注样本 ──
            _leftTabs = new TabControl { Dock = DockStyle.Fill };

            var tabQueue    = new TabPage("不确定性队列");
            var tabVerified = new TabPage("已标注样本");

            // --- 不确定性队列 tab ---
            var queueToolBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 3, 0, 0)
            };
            _refreshQueueBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "刷新",
                Width  = 60,
                Height = 24
            });
            _refreshQueueBtn.Click += (s, e) => LoadQueue();
            var queueHint = new Label
            {
                Text      = "模型最不确定的文本（需先导入文本）",
                Width     = 300,
                Height    = 24,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            queueToolBar.Controls.Add(_refreshQueueBtn);
            queueToolBar.Controls.Add(queueHint);

            _queueGrid = new DataGridView { Dock = DockStyle.Fill };
            NlpLabTheme.StyleGrid(_queueGrid);
            _queueGrid.Font = NlpLabTheme.Small;
            BuildQueueColumns();
            _queueGrid.SelectionChanged += OnQueueSelectionChanged;

            tabQueue.Controls.Add(_queueGrid);
            tabQueue.Controls.Add(queueToolBar);

            // --- 已标注样本 tab ---
            var verifiedToolBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 30,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 3, 0, 0)
            };
            var verifiedRefreshBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "刷新",
                Width  = 60,
                Height = 24
            });
            verifiedRefreshBtn.Click += (s, e) => LoadVerifiedSamples();
            _deleteVerifiedBtn = NlpLabTheme.MakeDanger(new Button
            {
                Text    = "删除所选",
                Width   = 75,
                Height  = 24,
                Enabled = false
            });
            _deleteVerifiedBtn.Click += OnDeleteVerified;
            var verifiedHint = new Label
            {
                Text      = "点击行可重新编辑标注",
                Width     = 200,
                Height    = 24,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary,
                TextAlign = ContentAlignment.MiddleLeft
            };
            verifiedToolBar.Controls.Add(verifiedRefreshBtn);
            verifiedToolBar.Controls.Add(_deleteVerifiedBtn);
            verifiedToolBar.Controls.Add(verifiedHint);

            _verifiedGrid = new DataGridView { Dock = DockStyle.Fill };
            NlpLabTheme.StyleGrid(_verifiedGrid);
            _verifiedGrid.Font = NlpLabTheme.Small;
            _verifiedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Text",     HeaderText = "文本摘要",   FillWeight = 55, ReadOnly = true });
            _verifiedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Entities", HeaderText = "实体数",     FillWeight = 15, ReadOnly = true });
            _verifiedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Source",   HeaderText = "来源",       FillWeight = 15, ReadOnly = true });
            _verifiedGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created",  HeaderText = "时间",       FillWeight = 15, ReadOnly = true });
            _verifiedGrid.SelectionChanged += OnVerifiedSelectionChanged;

            tabVerified.Controls.Add(_verifiedGrid);
            tabVerified.Controls.Add(verifiedToolBar);

            _leftTabs.TabPages.Add(tabQueue);
            _leftTabs.TabPages.Add(tabVerified);

            panel.Controls.Add(_leftTabs);
            panel.Controls.Add(statsBar);
            return panel;
        }

        private void BuildQueueColumns()
        {
            _queueGrid.Columns.Clear();
            _queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Text",    HeaderText = "文本摘要",  FillWeight = 60, ReadOnly = true });
            _queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Conf",    HeaderText = "置信度",   FillWeight = 20, ReadOnly = true });
            _queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Created", HeaderText = "时间",     FillWeight = 20, ReadOnly = true });
        }

        private Panel BuildRightPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4, 0, 0, 0) };

            var annotateSection = new GroupBox
            {
                Text   = "当前标注文本",
                Dock   = DockStyle.Top,
                Height = 260,
                Font   = NlpLabTheme.SectionTitle
            };

            _annotateBox = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                Font        = NlpLabTheme.TextInput,
                BackColor   = NlpLabTheme.BgInput,
                BorderStyle = BorderStyle.None
            };

            var annotateBtnBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Bottom,
                Height        = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 4, 0, 0)
            };

            _confirmAnnotateBtn = NlpLabTheme.MakeSuccess(new Button
            {
                Text    = "✓ 确认标注",
                Width   = 100,
                Height  = 28,
                Enabled = false
            });
            _confirmAnnotateBtn.Click += OnConfirmAnnotation;

            _skipBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text    = "跳过",
                Width   = 60,
                Height  = 28,
                Enabled = false
            });
            _skipBtn.Click += OnSkip;

            annotateBtnBar.Controls.AddRange(new Control[] { _confirmAnnotateBtn, _skipBtn });
            annotateSection.Controls.Add(_annotateBox);
            annotateSection.Controls.Add(annotateBtnBar);

            var editSection = new GroupBox
            {
                Text   = "编辑实体（点击队列中的文本开始标注）",
                Dock   = DockStyle.Top,
                Height = 160,
                Font   = NlpLabTheme.SectionTitle
            };

            _editGrid = new DataGridView { Dock = DockStyle.Fill };
            NlpLabTheme.StyleGrid(_editGrid);
            _editGrid.AllowUserToAddRows    = true;
            _editGrid.AllowUserToDeleteRows = true;
            _editGrid.Font = NlpLabTheme.Small;
            BuildEditGridColumns();
            editSection.Controls.Add(_editGrid);

            var trainSection = BuildTrainingSection();

            panel.Controls.Add(trainSection);
            panel.Controls.Add(editSection);
            panel.Controls.Add(annotateSection);
            return panel;
        }

        private void BuildEditGridColumns()
        {
            _editGrid.Columns.Clear();
            _editGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Text",  HeaderText = "实体文本", FillWeight = 40 });
            var typeCol = new DataGridViewComboBoxColumn { Name = "Type", HeaderText = "实体类型", FillWeight = 30, FlatStyle = FlatStyle.Flat };
            foreach (var t in _scenario.EntityTypes) typeCol.Items.Add(t);
            _editGrid.Columns.Add(typeCol);
            _editGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Start", HeaderText = "起始", FillWeight = 15 });
            _editGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "End",   HeaderText = "结束", FillWeight = 15 });
        }

        private GroupBox BuildTrainingSection()
        {
            var section = new GroupBox
            {
                Text = "增量训练",
                Dock = DockStyle.Fill,
                Font = NlpLabTheme.SectionTitle
            };

            var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            var paramBar = new FlowLayoutPanel
            {
                Dock          = DockStyle.Top,
                Height        = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding       = new Padding(0, 4, 0, 0)
            };

            var presetLabel = new Label
            {
                Text      = "训练预设：",
                Width     = 70,
                Height    = 28,
                TextAlign = ContentAlignment.MiddleLeft,
                Font      = NlpLabTheme.Body
            };
            _presetCombo = new ComboBox
            {
                Width         = 100,
                Height        = 28,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = NlpLabTheme.Small
            };
            _presetCombo.Items.AddRange(new object[] { "快速", "标准", "精细" });
            _presetCombo.SelectedIndex = 1;

            _trainBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text   = "开始训练",
                Width  = 90,
                Height = 28
            });
            _trainBtn.Click += OnTrain;

            _cancelTrainBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text    = "取消",
                Width   = 60,
                Height  = 28,
                Enabled = false
            });
            _cancelTrainBtn.Click += (s, e) => _trainCts?.Cancel();

            paramBar.Controls.AddRange(new Control[] { presetLabel, _presetCombo, _trainBtn, _cancelTrainBtn });

            _trainProgress = new ProgressBar
            {
                Dock    = DockStyle.Top,
                Height  = 6,
                Minimum = 0,
                Maximum = 100,
                Value   = 0,
                Style   = ProgressBarStyle.Continuous
            };

            _trainStatusLabel = new Label
            {
                Dock      = DockStyle.Top,
                Height    = 22,
                Text      = "就绪",
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary
            };

            _trainLog = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                Font        = NlpLabTheme.Mono,
                BackColor   = Color.FromArgb(248, 249, 250),
                ForeColor   = NlpLabTheme.TextPrimary,
                BorderStyle = BorderStyle.None
            };

            content.Controls.Add(_trainLog);
            content.Controls.Add(_trainStatusLabel);
            content.Controls.Add(_trainProgress);
            content.Controls.Add(paramBar);
            section.Controls.Add(content);
            return section;
        }

        // ── 队列管理 ─────────────────────────────────────────────────────────

        private void LoadQueue()
        {
            _queue = _engine.GetUncertainQueue(_scenario.Id, 30);
            _queueGrid.Rows.Clear();

            foreach (var entry in _queue)
            {
                string preview = entry.RawText.Length > 60
                    ? entry.RawText.Substring(0, 57) + "..."
                    : entry.RawText;
                _queueGrid.Rows.Add(preview, $"{entry.MinConfidence:P0}",
                    entry.CreatedAt.Length > 16 ? entry.CreatedAt.Substring(5, 11) : entry.CreatedAt);
            }

            RefreshStats();
        }

        private void LoadVerifiedSamples()
        {
            _verifiedSamples = _engine.GetAnnotatedTexts(_scenario.Id, verifiedOnly: true);
            _verifiedGrid.Rows.Clear();

            foreach (var sample in _verifiedSamples)
            {
                string preview = sample.RawText.Length > 60
                    ? sample.RawText.Substring(0, 57) + "..."
                    : sample.RawText;
                int entityCount = 0;
                try
                {
                    var anns = JsonConvert.DeserializeObject<List<ActiveEntityAnnotation>>(sample.AnnotationsJson);
                    entityCount = anns?.Count ?? 0;
                }
                catch { }
                string created = sample.CreatedAt.Length >= 16
                    ? sample.CreatedAt.Substring(5, 11)
                    : sample.CreatedAt;
                _verifiedGrid.Rows.Add(preview, entityCount, sample.Source, created);
            }

            // 切换到"已标注样本"Tab 以提醒用户（仅当队列为空时）
            if (_verifiedSamples.Count > 0 && _queue.Count == 0 && _leftTabs != null)
                _leftTabs.SelectedIndex = 1;
        }

        private void OnVerifiedSelectionChanged(object sender, EventArgs e)
        {
            if (_verifiedGrid.CurrentRow == null) return;
            int idx = _verifiedGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _verifiedSamples.Count) return;

            _currentVerified = _verifiedSamples[idx];
            _currentEntry    = null;

            _annotateBox.Text = _currentVerified.RawText;
            _currentAnnotations = DeserializeAnnotations(_currentVerified.AnnotationsJson);
            RefreshEditGrid();

            _confirmAnnotateBtn.Enabled = true;
            _skipBtn.Enabled            = false;
            _deleteVerifiedBtn.Enabled  = true;
        }

        private void OnDeleteVerified(object sender, EventArgs e)
        {
            if (_currentVerified == null) return;
            string preview = _currentVerified.RawText.Length > 60
                ? _currentVerified.RawText.Substring(0, 60) + "..."
                : _currentVerified.RawText;
            if (MessageBox.Show($"确定删除该标注样本？\n\n{preview}",
                "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _engine.DeleteAnnotatedText(_currentVerified.Id);
            _currentVerified = null;
            _annotateBox.Clear();
            _editGrid.Rows.Clear();
            _confirmAnnotateBtn.Enabled = false;
            _deleteVerifiedBtn.Enabled  = false;
            LoadVerifiedSamples();
            RefreshStats();
        }

        private void RefreshStats()
        {
            int verified = _engine.GetVerifiedCount(_scenario.Id);
            int pending  = _engine.GetPendingUncertainCount(_scenario.Id);
            string trainStatus = verified >= _engine.MinSamplesForTraining
                ? "✓ 可以训练"
                : $"还差 {_engine.MinSamplesForTraining - verified} 条（需 {_engine.MinSamplesForTraining} 条不同文本）";
            _statsLabel.Text = $"已标注 {verified} 条 | 待审核 {pending} 条 | {trainStatus}";

            _trainBtn.Enabled = verified >= _engine.MinSamplesForTraining;
            if (!_trainBtn.Enabled)
                _trainStatusLabel.Text = $"还需 {_engine.MinSamplesForTraining - verified} 条不同文本才能训练（每条唯一文本计 1 条，重复提交不累计）";

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    var metrics = _engine.EvaluateCurrentModel(_scenario.Id);
                    if (this.IsHandleCreated)
                    {
                        this.Invoke((Action)(() =>
                        {
                            _qualityLabel.Text = $"当前 F1: {metrics.F1:P1} | Precision: {metrics.Precision:P1} | Recall: {metrics.Recall:P1} | 样本 {metrics.SampleCount}";
                            _qualityLabel.ForeColor = metrics.F1 >= 0.95 ? Color.FromArgb(82, 196, 26)
                                : metrics.F1 >= 0.85 ? Color.DarkOrange : NlpLabTheme.Danger;
                        }));
                    }
                }
                catch { }
            });
        }

        private void OnQueueSelectionChanged(object sender, EventArgs e)
        {
            if (_queueGrid.CurrentRow == null) return;
            int idx = _queueGrid.CurrentRow.Index;
            if (idx < 0 || idx >= _queue.Count) return;

            _currentEntry = _queue[idx];
            _annotateBox.Text = _currentEntry.RawText;

            _currentAnnotations = DeserializeAnnotations(_currentEntry.PredictionsJson);
            RefreshEditGrid();

            _confirmAnnotateBtn.Enabled = true;
            _skipBtn.Enabled            = true;
        }

        private void RefreshEditGrid()
        {
            _editGrid.Rows.Clear();
            foreach (var ann in _currentAnnotations)
                _editGrid.Rows.Add(ann.Text, ann.EntityType, ann.StartIndex, ann.EndIndex);
        }

        private void OnConfirmAnnotation(object sender, EventArgs e)
        {
            string rawText = _currentEntry?.RawText ?? _currentVerified?.RawText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(rawText)) return;

            var confirmed = new List<ActiveEntityAnnotation>();
            foreach (DataGridViewRow row in _editGrid.Rows)
            {
                if (row.IsNewRow) continue;
                string text = row.Cells["Text"].Value?.ToString() ?? "";
                string type = row.Cells["Type"].Value?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(type))
                {
                    confirmed.Add(new ActiveEntityAnnotation
                    {
                        Text       = text,
                        EntityType = type,
                        StartIndex = int.TryParse(row.Cells["Start"].Value?.ToString(), out int s) ? s : 0,
                        EndIndex   = int.TryParse(row.Cells["End"].Value?.ToString(), out int en) ? en : 0,
                        Confidence = 1f,
                        IsManual   = true
                    });
                }
            }

            if (!ValidateConfirmedAnnotations(rawText, confirmed, out string error))
            {
                MessageBox.Show(error, "标注校验失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_currentEntry != null)
            {
                // 来自不确定性队列
                _engine.SubmitCorrection(rawText, confirmed, _scenario.Id,
                    _currentEntry.MinConfidence, _currentEntry.Id);

                AppendLog($"✓ 已标注（队列）：{rawText.Substring(0, Math.Min(40, rawText.Length))}...");

                int rowIdx = _queueGrid.CurrentRow?.Index ?? -1;
                if (rowIdx >= 0 && rowIdx < _queue.Count)
                {
                    _queue.RemoveAt(rowIdx);
                    _queueGrid.Rows.RemoveAt(rowIdx);
                }
                _currentEntry = null;
            }
            else if (_currentVerified != null)
            {
                // 来自已标注样本（重新标注）
                _engine.SubmitCorrection(rawText, confirmed, _scenario.Id, 1f);
                AppendLog($"✓ 已更新标注：{rawText.Substring(0, Math.Min(40, rawText.Length))}...");
                _currentVerified = null;
                LoadVerifiedSamples();
            }

            _annotateBox.Clear();
            _editGrid.Rows.Clear();
            _confirmAnnotateBtn.Enabled = false;
            _skipBtn.Enabled            = false;
            _deleteVerifiedBtn.Enabled  = false;
            RefreshStats();
        }

        private bool ValidateConfirmedAnnotations(
            string rawText,
            List<ActiveEntityAnnotation> annotations,
            out string error)
        {
            error = string.Empty;
            int textLength = rawText?.Length ?? 0;
            var validTypes = new HashSet<string>(_scenario.EntityTypes, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < annotations.Count; i++)
            {
                var ann = annotations[i];
                if (!validTypes.Contains(ann.EntityType))
                {
                    error = $"第 {i + 1} 行实体类型“{ann.EntityType}”不在当前场景标签中。";
                    return false;
                }

                if (ann.StartIndex < 0 || ann.EndIndex < ann.StartIndex || ann.EndIndex >= textLength)
                {
                    error = $"第 {i + 1} 行索引范围无效（{ann.StartIndex}-{ann.EndIndex}）。";
                    return false;
                }

                string expected = rawText.Substring(ann.StartIndex, ann.EndIndex - ann.StartIndex + 1);
                if (!string.Equals(expected, ann.Text, StringComparison.Ordinal))
                {
                    error = $"第 {i + 1} 行文本与索引不一致。建议改为“{expected}”。";
                    return false;
                }
            }

            var ordered = annotations
                .Select((ann, idx) => new { Ann = ann, Index = idx + 1 })
                .OrderBy(x => x.Ann.StartIndex)
                .ToList();
            for (int i = 1; i < ordered.Count; i++)
            {
                if (ordered[i].Ann.StartIndex <= ordered[i - 1].Ann.EndIndex)
                {
                    error = $"第 {ordered[i - 1].Index} 行与第 {ordered[i].Index} 行存在重叠区间。";
                    return false;
                }
            }

            return true;
        }

        private void OnSkip(object sender, EventArgs e)
        {
            if (_currentEntry == null) return;

            var result = MessageBox.Show(
                $"确定跳过该文本？\n\n{_currentEntry.RawText.Substring(0, Math.Min(80, _currentEntry.RawText.Length))}...",
                "确认跳过",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            if (result != DialogResult.Yes) return;

            AppendLog($"→ 跳过：{_currentEntry.RawText.Substring(0, Math.Min(30, _currentEntry.RawText.Length))}...");
            _engine.MarkUncertainSkipped(_currentEntry.Id, "user_skip");

            int rowIdx = _queueGrid.CurrentRow?.Index ?? -1;
            if (rowIdx >= 0 && rowIdx < _queue.Count)
            {
                _queue.RemoveAt(rowIdx);
                _queueGrid.Rows.RemoveAt(rowIdx);
            }
            _currentEntry = null;
            _annotateBox.Clear();
            _confirmAnnotateBtn.Enabled = false;
            _skipBtn.Enabled = false;
        }

        // ── 训练 ─────────────────────────────────────────────────────────────

        private async void OnTrain(object sender, EventArgs e)
        {
            _trainBtn.Enabled       = false;
            _cancelTrainBtn.Enabled = true;
            _trainProgress.Value    = 0;
            _trainCts               = new CancellationTokenSource();
            var ct = _trainCts.Token;

            var preset = _presetCombo.SelectedIndex switch
            {
                0 => TrainingParameters.Fast(),
                2 => TrainingParameters.Fine(),
                _ => TrainingParameters.Standard()
            };

            AppendLog("========== 开始增量训练 ==========");

            var progress = new Progress<(string Stage, string Detail, double Percent)>(info =>
            {
                if (this.IsHandleCreated)
                {
                    this.Invoke((Action)(() =>
                    {
                        _trainProgress.Value  = (int)Math.Min(100, info.Percent);
                        _trainStatusLabel.Text = $"[{info.Stage}] {info.Detail}";
                        AppendLog($"[{info.Stage}] {info.Detail}");
                    }));
                }
            });

            try
            {
                var result = await System.Threading.Tasks.Task.Run(
                    () => _engine.TrainIncremental(_scenario.Id, preset, progress, ct), ct);

                this.Invoke((Action)(() =>
                {
                    _trainProgress.Value = 100;
                    AppendLog(result.Message);

                    if (result.MetricsBefore != null && result.MetricsAfter != null)
                    {
                        AppendLog($"  F1:        {result.MetricsBefore.F1:P2} → {result.MetricsAfter.F1:P2}");
                        AppendLog($"  Precision: {result.MetricsBefore.Precision:P2} → {result.MetricsAfter.Precision:P2}");
                        AppendLog($"  Recall:    {result.MetricsBefore.Recall:P2} → {result.MetricsAfter.Recall:P2}");

                        _trainStatusLabel.Text      = result.ModelApplied
                            ? "训练成功，已加载最新模型"
                            : "训练完成，但新模型未优于当前模型，未应用";
                        _trainStatusLabel.ForeColor = result.ModelApplied
                            ? Color.FromArgb(82, 196, 26)
                            : Color.DarkOrange;

                        if (result.ReachedTarget)
                            AppendLog("F1 >= 95%，模型已达到目标质量！");
                    }

                    AppendLog($"耗时: {result.DurationSeconds:F1}s");
                    AppendLog("===========================================");

                    RefreshStats();
                    TrainingCompleted?.Invoke();
                    TrainingCompletedDetailed?.Invoke(result);
                }));
            }
            catch (OperationCanceledException)
            {
                this.Invoke((Action)(() =>
                {
                    AppendLog("训练已取消");
                    _trainStatusLabel.Text = "已取消";
                }));
            }
            catch (Exception ex)
            {
                this.Invoke((Action)(() =>
                {
                    AppendLog($"训练异常: {ex.Message}");
                    _trainStatusLabel.Text      = "训练失败";
                    _trainStatusLabel.ForeColor = NlpLabTheme.Danger;
                }));
            }
            finally
            {
                this.Invoke((Action)(() =>
                {
                    _trainBtn.Enabled       = true;
                    _cancelTrainBtn.Enabled = false;
                }));
            }
        }

        private void AppendLog(string line)
        {
            if (!this.IsHandleCreated) return;
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => AppendLog(line)));
                return;
            }
            _trainLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}\n");
            _trainLog.ScrollToCaret();
        }

        private static List<ActiveEntityAnnotation> DeserializeAnnotations(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<ActiveEntityAnnotation>();
            try { return JsonConvert.DeserializeObject<List<ActiveEntityAnnotation>>(json) ?? new List<ActiveEntityAnnotation>(); }
            catch { return new List<ActiveEntityAnnotation>(); }
        }
    }
}
