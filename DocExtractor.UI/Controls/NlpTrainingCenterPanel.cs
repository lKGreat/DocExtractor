using System;
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
    /// Dedicated training center for incremental model training and lifecycle visibility.
    /// </summary>
    internal class NlpTrainingCenterPanel : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private NlpScenario _scenario;
        private CancellationTokenSource? _trainCts;

        private ComboBox _presetCombo = null!;
        private Button _trainBtn = null!;
        private Button _cancelBtn = null!;
        private ProgressBar _progress = null!;
        private Label _statusLabel = null!;
        private Label _modelFreshnessLabel = null!;
        private Label _currentModelLabel = null!;
        private DataGridView _historyGrid = null!;
        private RichTextBox _log = null!;

        public event Action<LearningSessionResult>? TrainingCompletedDetailed;

        public NlpTrainingCenterPanel(ActiveLearningEngine engine, NlpScenario scenario)
        {
            _engine = engine;
            _scenario = scenario;
            BuildLayout();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario = scenario;
            RefreshView();
        }

        public void OnActivated() => RefreshView();

        public bool CanStartTraining => _engine.GetVerifiedCount(_scenario.Id) >= _engine.MinSamplesForTraining;

        public string TrainingReadinessMessage
        {
            get
            {
                int verified = _engine.GetVerifiedCount(_scenario.Id);
                return verified >= _engine.MinSamplesForTraining
                    ? $"训练就绪（{verified}/{_engine.MinSamplesForTraining}）"
                    : $"样本不足，尚需 {_engine.MinSamplesForTraining - verified} 条";
            }
        }

        public void TriggerTraining(int presetIndex = 1)
        {
            if (!CanStartTraining)
            {
                MessageBox.Show(TrainingReadinessMessage, "无法训练", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (presetIndex < 0 || presetIndex >= _presetCombo.Items.Count) presetIndex = 1;
            _presetCombo.SelectedIndex = presetIndex;
            OnStartTraining(this, EventArgs.Empty);
        }

        private void BuildLayout()
        {
            Dock = DockStyle.Fill;
            Padding = new Padding(8);

            var root = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal
            };
            NlpLabTheme.SetSplitterDistanceDeferred(root, 0.56, panel1Min: 220, panel2Min: 180);

            root.Panel1.Controls.Add(BuildTrainingPanel());
            root.Panel2.Controls.Add(BuildHistoryPanel());
            Controls.Add(root);
        }

        private Control BuildTrainingPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(0, 3, 0, 0)
            };
            topBar.Controls.Add(new Label
            {
                Text = "训练预设：",
                Width = 70,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft
            });

            _presetCombo = new ComboBox
            {
                Width = 100,
                Height = 26,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _presetCombo.Items.AddRange(new object[] { "快速", "标准", "精细" });
            _presetCombo.SelectedIndex = 1;

            _trainBtn = NlpLabTheme.MakePrimary(new Button
            {
                Text = "开始训练",
                Width = 90,
                Height = 26
            });
            _trainBtn.Click += OnStartTraining;

            _cancelBtn = NlpLabTheme.MakeGhost(new Button
            {
                Text = "取消",
                Width = 60,
                Height = 26,
                Enabled = false
            });
            _cancelBtn.Click += (s, e) => _trainCts?.Cancel();

            topBar.Controls.Add(_presetCombo);
            topBar.Controls.Add(_trainBtn);
            topBar.Controls.Add(_cancelBtn);

            _progress = new ProgressBar
            {
                Dock = DockStyle.Top,
                Height = 6,
                Minimum = 0,
                Maximum = 100
            };

            _statusLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = NlpLabTheme.TextTertiary,
                Text = "就绪"
            };

            _currentModelLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                ForeColor = NlpLabTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _modelFreshnessLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 24,
                Font = NlpLabTheme.BodyBold,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _log = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                Font = NlpLabTheme.Mono,
                BackColor = Color.FromArgb(248, 249, 250),
                BorderStyle = BorderStyle.None
            };

            panel.Controls.Add(_log);
            panel.Controls.Add(_currentModelLabel);
            panel.Controls.Add(_modelFreshnessLabel);
            panel.Controls.Add(_statusLabel);
            panel.Controls.Add(_progress);
            panel.Controls.Add(topBar);
            return panel;
        }

        private Control BuildHistoryPanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            panel.Controls.Add(new Label
            {
                Text = "训练历史（应用结果）",
                Dock = DockStyle.Top,
                Height = 24,
                Font = NlpLabTheme.SectionTitle
            });

            _historyGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            NlpLabTheme.StyleGrid(_historyGrid);
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No", HeaderText = "#", FillWeight = 8 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1Before", HeaderText = "训前F1", FillWeight = 14 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1After", HeaderText = "训后F1", FillWeight = 14 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Comparison", HeaderText = "相对比较", FillWeight = 18 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Applied", HeaderText = "是否应用", FillWeight = 14 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AppliedAt", HeaderText = "应用时间", FillWeight = 16 });
            _historyGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "训练时间", FillWeight = 16 });
            panel.Controls.Add(_historyGrid);
            return panel;
        }

        private async void OnStartTraining(object? sender, EventArgs e)
        {
            if (!CanStartTraining)
            {
                MessageBox.Show(TrainingReadinessMessage, "无法训练", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshView();
                return;
            }

            _trainBtn.Enabled = false;
            _cancelBtn.Enabled = true;
            _progress.Value = 0;
            _trainCts = new CancellationTokenSource();

            var preset = _presetCombo.SelectedIndex switch
            {
                0 => TrainingParameters.Fast(),
                2 => TrainingParameters.Fine(),
                _ => TrainingParameters.Standard()
            };

            AppendLog("========== 训练开始 ==========");

            var progress = new Progress<(string Stage, string Detail, double Percent)>(info =>
            {
                if (!IsHandleCreated) return;
                Invoke((Action)(() =>
                {
                    _progress.Value = (int)Math.Min(100, info.Percent);
                    _statusLabel.Text = $"[{info.Stage}] {info.Detail}";
                }));
            });

            try
            {
                var result = await System.Threading.Tasks.Task.Run(
                    () => _engine.TrainIncremental(_scenario.Id, preset, progress, _trainCts.Token),
                    _trainCts.Token);

                Invoke((Action)(() =>
                {
                    _progress.Value = 100;
                    _statusLabel.Text = result.ModelApplied
                        ? "训练成功，已加载最新模型"
                        : "训练完成，未应用新模型";
                    _statusLabel.ForeColor = result.ModelApplied ? Color.FromArgb(82, 196, 26) : Color.DarkOrange;
                    AppendLog(result.Message);
                    AppendLog($"耗时: {result.DurationSeconds:F1}s");
                    AppendLog("========== 训练结束 ==========");
                    RefreshView();
                    TrainingCompletedDetailed?.Invoke(result);
                }));
            }
            catch (OperationCanceledException)
            {
                Invoke((Action)(() =>
                {
                    _statusLabel.Text = "训练已取消";
                    _statusLabel.ForeColor = Color.DarkOrange;
                    AppendLog("训练已取消");
                }));
            }
            catch (Exception ex)
            {
                Invoke((Action)(() =>
                {
                    _statusLabel.Text = "训练失败";
                    _statusLabel.ForeColor = NlpLabTheme.Danger;
                    AppendLog($"训练失败: {ex.Message}");
                }));
            }
            finally
            {
                Invoke((Action)(() =>
                {
                    _trainBtn.Enabled = true;
                    _cancelBtn.Enabled = false;
                }));
            }
        }

        private void RefreshView()
        {
            _trainBtn.Enabled = CanStartTraining;
            if (!_trainBtn.Enabled)
            {
                _statusLabel.Text = TrainingReadinessMessage;
                _statusLabel.ForeColor = Color.DarkOrange;
            }

            string currentTag = _engine.GetCurrentModelTag();
            var latestApplied = _engine.GetLatestAppliedLearningSession(_scenario.Id);
            bool isLatestLoaded = latestApplied != null &&
                string.Equals(currentTag, latestApplied.ModelTag, StringComparison.OrdinalIgnoreCase);

            _currentModelLabel.Text = $"当前加载：{currentTag}";
            _modelFreshnessLabel.Text = latestApplied == null
                ? "最新状态：尚无已应用训练会话"
                : isLatestLoaded
                    ? "最新状态：已加载最新模型"
                    : "最新状态：当前未加载最新已应用模型";
            _modelFreshnessLabel.ForeColor = latestApplied == null
                ? Color.DarkOrange
                : isLatestLoaded ? Color.FromArgb(82, 196, 26) : NlpLabTheme.Danger;

            RefreshHistory();
        }

        private void RefreshHistory()
        {
            _historyGrid.Rows.Clear();
            var sessions = _engine.GetLearningSessions(_scenario.Id);
            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var before = TryDeserializeMetrics(s.MetricsBeforeJson);
                var after = TryDeserializeMetrics(s.MetricsAfterJson);
                string comparison = s.PassedComparison ? "优于当前" : "未优于当前";
                string applied = s.ModelApplied ? "已应用" : "未应用";
                var row = _historyGrid.Rows.Add(
                    i + 1,
                    before != null ? $"{before.F1:P2}" : "—",
                    after != null ? $"{after.F1:P2}" : "—",
                    comparison,
                    applied,
                    string.IsNullOrWhiteSpace(s.AppliedAt) ? "—" : s.AppliedAt,
                    s.TrainedAt.Length >= 16 ? s.TrainedAt.Substring(0, 16) : s.TrainedAt);
                _historyGrid.Rows[row].DefaultCellStyle.ForeColor = s.ModelApplied
                    ? Color.FromArgb(82, 196, 26)
                    : Color.DarkOrange;
            }
        }

        private static NlpQualityMetrics? TryDeserializeMetrics(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return null;
            try
            {
                return JsonConvert.DeserializeObject<NlpQualityMetrics>(json);
            }
            catch
            {
                return null;
            }
        }

        private void AppendLog(string text)
        {
            _log.AppendText($"[{DateTime.Now:HH:mm:ss}] {text}\n");
            _log.ScrollToCaret();
        }
    }
}
