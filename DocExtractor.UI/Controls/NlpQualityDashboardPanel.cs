using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Data.ActiveLearning;
using DocExtractor.Data.Repositories;
using Newtonsoft.Json;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// 质量仪表盘页面
    /// 展示 F1/Precision/Recall 历史趋势、分项指标、泛化对比
    /// </summary>
    internal class NlpQualityDashboardPanel : UserControl
    {
        private readonly ActiveLearningEngine _engine;
        private readonly ScenarioManager _scenarioMgr;
        private NlpScenario _scenario;

        // ── 控件字段 ─────────────────────────────────────────────────────────
        private Button _refreshBtn           = null!;
        private Label  _summaryLabel         = null!;
        private MetricsTrendChart _trendChart = null!;
        private DataGridView _sessionGrid    = null!;
        private DataGridView _perTypeGrid    = null!;
        private DataGridView _generalizationGrid = null!;
        private Label _generalizationNote    = null!;

        public NlpQualityDashboardPanel(ActiveLearningEngine engine, ScenarioManager scenarioMgr, NlpScenario scenario)
        {
            _engine      = engine;
            _scenarioMgr = scenarioMgr;
            _scenario    = scenario;
            InitializeLayout();
        }

        public void SetScenario(NlpScenario scenario)
        {
            _scenario = scenario;
            Refresh_();
        }

        public void OnActivated() => Refresh_();

        // ── 布局 ──────────────────────────────────────────────────────────────

        private void InitializeLayout()
        {
            this.Dock    = DockStyle.Fill;
            this.Padding = new Padding(8);
            this.Font    = NlpLabTheme.Body;

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 40 };

            var title = new Label
            {
                Text      = "模型质量仪表盘",
                Dock      = DockStyle.Left,
                Width     = 160,
                Height    = 36,
                Font      = NlpLabTheme.Title,
                ForeColor = NlpLabTheme.Primary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _refreshBtn = NlpLabTheme.MakeDefault(new Button
            {
                Text   = "刷新",
                Width  = 70,
                Height = 30,
                Dock   = DockStyle.Right
            });
            _refreshBtn.Click += (s, e) => Refresh_();

            _summaryLabel = new Label
            {
                Text      = "—",
                Dock      = DockStyle.Fill,
                Height    = 36,
                Font      = NlpLabTheme.Body,
                ForeColor = NlpLabTheme.TextSecondary,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            };

            toolbar.Controls.Add(_refreshBtn);
            toolbar.Controls.Add(_summaryLabel);
            toolbar.Controls.Add(title);

            var mainSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Height      = 800
            };
            NlpLabTheme.SetSplitterDistanceDeferred(mainSplit, 0.40, panel1Min: 150, panel2Min: 200);

            _trendChart = new MetricsTrendChart { Dock = DockStyle.Fill };
            mainSplit.Panel1.Controls.Add(_trendChart);

            var bottomSplit = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                Width       = 1200
            };
            NlpLabTheme.SetSplitterDistanceDeferred(bottomSplit, 0.55, panel1Min: 250, panel2Min: 200);

            var leftBottom = new SplitContainer
            {
                Dock        = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                Height      = 600
            };
            NlpLabTheme.SetSplitterDistanceDeferred(leftBottom, 0.55, panel1Min: 100, panel2Min: 100);

            leftBottom.Panel1.Controls.Add(BuildSessionHistorySection());
            leftBottom.Panel2.Controls.Add(BuildPerTypeSection());
            bottomSplit.Panel1.Controls.Add(leftBottom);
            bottomSplit.Panel2.Controls.Add(BuildGeneralizationSection());

            mainSplit.Panel2.Controls.Add(bottomSplit);

            this.Controls.Add(mainSplit);
            this.Controls.Add(toolbar);
        }

        private Panel BuildSessionHistorySection()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var title = new Label
            {
                Text      = "训练历史记录",
                Dock      = DockStyle.Top,
                Height    = 26,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _sessionGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            NlpLabTheme.StyleGrid(_sessionGrid);

            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "No",       HeaderText = "#",      FillWeight = 8 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1Before",  HeaderText = "训前F1", FillWeight = 18 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1After",   HeaderText = "训后F1", FillWeight = 18 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Improved",  HeaderText = "提升",   FillWeight = 14 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Samples",   HeaderText = "样本",   FillWeight = 14 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Duration",  HeaderText = "耗时",   FillWeight = 14 });
            _sessionGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date",      HeaderText = "时间",   FillWeight = 14 });

            panel.Controls.Add(_sessionGrid);
            panel.Controls.Add(title);
            return panel;
        }

        private Panel BuildPerTypeSection()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var title = new Label
            {
                Text      = "实体类型分项指标",
                Dock      = DockStyle.Top,
                Height    = 26,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _perTypeGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            NlpLabTheme.StyleGrid(_perTypeGrid);

            _perTypeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type",   HeaderText = "实体类型", FillWeight = 35 });
            _perTypeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1",     HeaderText = "F1",      FillWeight = 22 });
            _perTypeGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "状态",    FillWeight = 43 });

            panel.Controls.Add(_perTypeGrid);
            panel.Controls.Add(title);
            return panel;
        }

        private Panel BuildGeneralizationSection()
        {
            var panel = new Panel { Dock = DockStyle.Fill };

            var title = new Label
            {
                Text      = "跨场景泛化能力对比",
                Dock      = DockStyle.Top,
                Height    = 26,
                Font      = NlpLabTheme.SectionTitle,
                ForeColor = NlpLabTheme.TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _generalizationNote = new Label
            {
                Text      = "（评估当前模型在其他场景标注数据上的 F1，验证泛化能力）",
                Dock      = DockStyle.Top,
                Height    = 20,
                Font      = NlpLabTheme.Small,
                ForeColor = NlpLabTheme.TextTertiary
            };

            _generalizationGrid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
            NlpLabTheme.StyleGrid(_generalizationGrid);

            _generalizationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Scenario", HeaderText = "场景",   FillWeight = 40 });
            _generalizationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Samples",  HeaderText = "样本数", FillWeight = 20 });
            _generalizationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "F1",       HeaderText = "F1",     FillWeight = 20 });
            _generalizationGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note",     HeaderText = "说明",   FillWeight = 20 });

            panel.Controls.Add(_generalizationGrid);
            panel.Controls.Add(_generalizationNote);
            panel.Controls.Add(title);
            return panel;
        }

        // ── 数据刷新 ─────────────────────────────────────────────────────────

        private void Refresh_()
        {
            RefreshSummary();
            RefreshSessionHistory();
            RefreshPerType();
            RefreshGeneralization();
        }

        private void RefreshSummary()
        {
            var metrics = _engine.EvaluateCurrentModel(_scenario.Id);
            int verified = _engine.GetVerifiedCount(_scenario.Id);

            string goal  = metrics.F1 >= 0.95 ? "✓ 已达标" : $"距目标还差 {(0.95 - metrics.F1):P1}";
            _summaryLabel.Text = $"当前场景：{_scenario.Name} | 样本 {verified} 条 | F1: {metrics.F1:P2} | {goal}";
            _summaryLabel.ForeColor = metrics.F1 >= 0.95
                ? Color.FromArgb(82, 196, 26)
                : metrics.F1 >= 0.80
                    ? Color.DarkOrange
                    : NlpLabTheme.Danger;
        }

        private void RefreshSessionHistory()
        {
            _sessionGrid.Rows.Clear();
            var sessions = _engine.GetLearningSessions(_scenario.Id);

            _trendChart.SetData(sessions);

            for (int i = 0; i < sessions.Count; i++)
            {
                var s = sessions[i];
                var mBefore = TryDeserialize(s.MetricsBeforeJson);
                var mAfter  = TryDeserialize(s.MetricsAfterJson);

                string improved = s.IsImproved ? "↑ 提升" : "→ 持平/降";
                string date = s.TrainedAt.Length >= 16 ? s.TrainedAt.Substring(0, 16) : s.TrainedAt;

                var row = _sessionGrid.Rows.Add(
                    i + 1,
                    mBefore != null ? $"{mBefore.F1:P2}" : "—",
                    mAfter  != null ? $"{mAfter.F1:P2}"  : "—",
                    improved,
                    s.SampleCountAfter,
                    $"{s.DurationSeconds:F0}s",
                    date);

                _sessionGrid.Rows[row].DefaultCellStyle.ForeColor =
                    s.IsImproved ? Color.FromArgb(82, 196, 26) : Color.DarkOrange;
            }
        }

        private void RefreshPerType()
        {
            _perTypeGrid.Rows.Clear();
            var metrics = _engine.EvaluateCurrentModel(_scenario.Id);

            foreach (var type in _scenario.EntityTypes)
            {
                double f1 = metrics.PerTypeF1.TryGetValue(type, out var v) ? v : 0.0;
                string status = f1 >= 0.95 ? "优秀" : f1 >= 0.80 ? "良好" : f1 > 0 ? "需改善" : "数据不足";
                var row = _perTypeGrid.Rows.Add(type, $"{f1:P2}", status);

                Color fg = f1 >= 0.95 ? Color.FromArgb(82, 196, 26) : f1 >= 0.80 ? Color.DarkOrange : NlpLabTheme.Danger;
                _perTypeGrid.Rows[row].DefaultCellStyle.ForeColor = fg;
            }
        }

        private void RefreshGeneralization()
        {
            _generalizationGrid.Rows.Clear();
            var allScenarios = _scenarioMgr.GetAllScenarios();

            foreach (var sc in allScenarios)
            {
                int sampleCount = _engine.GetVerifiedCount(sc.Id);
                string note   = sc.Id == _scenario.Id ? "（当前）" : "";
                double f1     = 0;

                if (sampleCount >= 5)
                {
                    try
                    {
                        var m = _engine.EvaluateCurrentModel(sc.Id);
                        f1 = m.F1;
                    }
                    catch { }
                }

                string f1Str = sampleCount >= 5 ? $"{f1:P2}" : "样本不足";
                var row = _generalizationGrid.Rows.Add(sc.Name, sampleCount, f1Str, note);
                if (sc.Id == _scenario.Id)
                    _generalizationGrid.Rows[row].DefaultCellStyle.BackColor = Color.FromArgb(230, 244, 255);
            }
        }

        private static NlpQualityMetrics? TryDeserialize(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
            try { return JsonConvert.DeserializeObject<NlpQualityMetrics>(json); }
            catch { return null; }
        }
    }

    // ── 趋势图控件 ────────────────────────────────────────────────────────────

    internal class MetricsTrendChart : Control
    {
        private List<NlpLearningSession> _sessions = new List<NlpLearningSession>();
        private static readonly Color ColorF1     = NlpLabTheme.Primary;
        private static readonly Color ColorP      = Color.FromArgb(82, 196, 26);
        private static readonly Color ColorR      = Color.FromArgb(250, 173, 20);
        private static readonly Color ColorTarget = NlpLabTheme.Danger;

        public MetricsTrendChart()
        {
            DoubleBuffered = true;
            ResizeRedraw   = true;
            BackColor      = Color.White;
        }

        public void SetData(List<NlpLearningSession> sessions)
        {
            _sessions = sessions;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(50, 16, Width - 70, Height - 50);
            if (rect.Width <= 0 || rect.Height <= 0) return;

            g.FillRectangle(Brushes.White, ClientRectangle);

            DrawHorizontalGuide(g, rect, 0.95f, ColorTarget, "目标 95%");
            DrawHorizontalGuide(g, rect, 0.80f, NlpLabTheme.Border, "80%");
            DrawHorizontalGuide(g, rect, 0.60f, Color.FromArgb(230, 230, 230), "60%");

            using var axisPen = new Pen(NlpLabTheme.Border);
            g.DrawLine(axisPen, rect.Left, rect.Top,    rect.Left, rect.Bottom);
            g.DrawLine(axisPen, rect.Left, rect.Bottom, rect.Right, rect.Bottom);

            using var labelFont  = new Font("微软雅黑", 7.5F);
            using var labelBrush = new SolidBrush(NlpLabTheme.TextTertiary);
            for (int pct = 0; pct <= 100; pct += 20)
            {
                float y = rect.Bottom - rect.Height * pct / 100f;
                g.DrawString($"{pct}%", labelFont, labelBrush, 2, y - 7);
            }

            if (_sessions.Count == 0)
            {
                using var hintFont = NlpLabTheme.Body;
                g.DrawString("暂无训练记录，完成至少一次训练后将显示质量趋势", hintFont,
                    Brushes.Gray, rect.Left + 30, rect.Top + rect.Height / 2 - 10);
                DrawLegend(g, rect);
                return;
            }

            DrawMetricLine(g, rect, _sessions, s => TryGetMetrics(s.MetricsAfterJson)?.F1 ?? 0, ColorF1);
            DrawMetricLine(g, rect, _sessions, s => TryGetMetrics(s.MetricsAfterJson)?.Precision ?? 0, ColorP);
            DrawMetricLine(g, rect, _sessions, s => TryGetMetrics(s.MetricsAfterJson)?.Recall ?? 0, ColorR);

            for (int i = 0; i < _sessions.Count; i++)
            {
                float x = GetX(rect, i, _sessions.Count);
                g.DrawString($"#{i + 1}", labelFont, labelBrush, x - 8, rect.Bottom + 4);
            }

            g.DrawString("训练迭代次数", labelFont, labelBrush, rect.Right - 60, rect.Bottom + 16);

            DrawLegend(g, rect);
        }

        private static void DrawHorizontalGuide(Graphics g, Rectangle rect, float value, Color color, string label)
        {
            float y = rect.Bottom - rect.Height * value;
            using var pen = new Pen(color, 1) { DashStyle = DashStyle.Dash };
            g.DrawLine(pen, rect.Left, y, rect.Right, y);

            using var font  = new Font("微软雅黑", 7F);
            using var brush = new SolidBrush(color);
            g.DrawString(label, font, brush, rect.Right + 2, y - 7);
        }

        private static void DrawMetricLine(
            Graphics g,
            Rectangle rect,
            List<NlpLearningSession> sessions,
            Func<NlpLearningSession, double> getValue,
            Color color)
        {
            using var pen   = new Pen(color, 2f);
            using var brush = new SolidBrush(color);

            PointF? prev = null;
            for (int i = 0; i < sessions.Count; i++)
            {
                double val = getValue(sessions[i]);
                float x = GetX(rect, i, sessions.Count);
                float y = rect.Bottom - rect.Height * (float)val;
                var pt = new PointF(x, y);

                if (prev.HasValue)
                    g.DrawLine(pen, prev.Value, pt);

                g.FillEllipse(brush, x - 4, y - 4, 8, 8);
                prev = pt;
            }
        }

        private static float GetX(Rectangle rect, int index, int total)
        {
            if (total <= 1) return rect.Left + rect.Width / 2f;
            return rect.Left + rect.Width * index / (float)(total - 1);
        }

        private static void DrawLegend(Graphics g, Rectangle rect)
        {
            var items = new[] { ("F1", ColorF1), ("Precision", ColorP), ("Recall", ColorR) };
            using var font = new Font("微软雅黑", 7.5F);
            float x = rect.Left + 10;
            float y = rect.Top + 4;
            foreach (var (label, color) in items)
            {
                using var brush = new SolidBrush(color);
                g.FillRectangle(brush, x, y + 2, 14, 10);
                g.DrawString(label, font, Brushes.Black, x + 18, y);
                x += 75;
            }
        }

        private static NlpQualityMetrics? TryGetMetrics(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json == "{}") return null;
            try { return JsonConvert.DeserializeObject<NlpQualityMetrics>(json); }
            catch { return null; }
        }
    }
}
