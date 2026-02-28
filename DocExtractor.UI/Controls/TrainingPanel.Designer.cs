using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    partial class TrainingPanel
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this._layout = new TableLayoutPanel();
            this._statsGroup = new GroupBox();
            this._statsFlow = new FlowLayoutPanel();
            this._colSampleCountLabel = new Label();
            this._nerSampleCountLabel = new Label();
            this._sectionSampleCountLabel = new Label();
            this._knowledgeCountLabel = new Label();
            this._modelHealthLabel = new Label();
            this._genFromKnowledgeBtn = new AntdUI.Button();
            this._importCsvBtn = new AntdUI.Button();
            this._importSectionWordBtn = new AntdUI.Button();
            this._columnErrorAnalysisBtn = new AntdUI.Button();
            this._paramsGroup = new GroupBox();
            this._paramsLayout = new TableLayoutPanel();
            this._presetCombo = new ComboBox();
            this._cvFoldsSpinner = new NumericUpDown();
            this._testFractionSpinner = new NumericUpDown();
            this._augmentCheckbox = new CheckBox();
            this._colEpochsSpinner = new NumericUpDown();
            this._colBatchSpinner = new NumericUpDown();
            this._nerEpochsSpinner = new NumericUpDown();
            this._nerBatchSpinner = new NumericUpDown();
            this._secTreesSpinner = new NumericUpDown();
            this._secLeavesSpinner = new NumericUpDown();
            this._secMinLeafSpinner = new NumericUpDown();
            this._trainBtnFlow = new FlowLayoutPanel();
            this._trainUnifiedBtn = new AntdUI.Button();
            this._cancelTrainBtn = new AntdUI.Button();
            this._evalPanel = new TableLayoutPanel();
            this._evalLabel = new Label();
            this._evalCompareLabel = new Label();
            this._trainLogPanel = new Panel();
            this._trainProgressBar = new ProgressBar();
            this._trainLogBox = new RichTextBox();

            this.SuspendLayout();
            foreach (var s in new System.Windows.Forms.NumericUpDown[] { _cvFoldsSpinner, _testFractionSpinner, _colEpochsSpinner, _colBatchSpinner, _nerEpochsSpinner, _nerBatchSpinner, _secTreesSpinner, _secLeavesSpinner, _secMinLeafSpinner })
                ((System.ComponentModel.ISupportInitialize)s).BeginInit();

            // ── Main Layout ───────────────────────────────────────────────────
            this._layout.Dock = DockStyle.Fill;
            this._layout.RowCount = 5;
            this._layout.ColumnCount = 1;
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 160));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 55));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // ── Stats Group ───────────────────────────────────────────────────
            this._statsGroup.Text = "训练数据统计";
            this._statsGroup.Dock = DockStyle.Fill;

            this._statsFlow.Dock = DockStyle.Fill;
            this._statsFlow.Padding = new Padding(8);

            this._colSampleCountLabel.Text = "列名分类样本：0 条";
            this._colSampleCountLabel.Size = new Size(200, 30);
            this._colSampleCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._nerSampleCountLabel.Text = "NER 标注样本：0 条";
            this._nerSampleCountLabel.Size = new Size(200, 30);
            this._nerSampleCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._sectionSampleCountLabel.Text = "章节标题样本：0 条";
            this._sectionSampleCountLabel.Size = new Size(200, 30);
            this._sectionSampleCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._knowledgeCountLabel.Text = "推荐知识库：0 条";
            this._knowledgeCountLabel.Size = new Size(200, 30);
            this._knowledgeCountLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this._modelHealthLabel.Text = "模型健康度：--";
            this._modelHealthLabel.Size = new Size(760, 30);
            this._modelHealthLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._modelHealthLabel.ForeColor = Color.FromArgb(32, 32, 32);

            this._genFromKnowledgeBtn.Text = "从知识库生成训练数据";
            this._genFromKnowledgeBtn.Type = AntdUI.TTypeMini.Primary;
            this._genFromKnowledgeBtn.Size = new Size(175, 30);

            this._importCsvBtn.Text = "导入 CSV/Excel 标注";
            this._importCsvBtn.Size = new Size(160, 30);

            this._importSectionWordBtn.Text = "从 Word 导入章节标注";
            this._importSectionWordBtn.Size = new Size(170, 30);

            this._columnErrorAnalysisBtn.Text = "列名错误分析";
            this._columnErrorAnalysisBtn.Size = new Size(120, 30);

            this._statsFlow.Controls.AddRange(new Control[]
            {
                this._colSampleCountLabel, this._nerSampleCountLabel,
                this._sectionSampleCountLabel, this._knowledgeCountLabel,
                this._modelHealthLabel,
                this._genFromKnowledgeBtn, this._importCsvBtn, this._importSectionWordBtn, this._columnErrorAnalysisBtn
            });
            this._statsGroup.Controls.Add(this._statsFlow);

            // ── Params Group ──────────────────────────────────────────────────
            this._paramsGroup.Text = "训练参数";
            this._paramsGroup.Dock = DockStyle.Fill;

            this._paramsLayout.Dock = DockStyle.Fill;
            this._paramsLayout.ColumnCount = 10;
            this._paramsLayout.RowCount = 3;
            this._paramsLayout.Padding = new Padding(4);
            for (int i = 0; i < 3; i++) this._paramsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            int[] colWidths = { 82, 66, 82, 66, 82, 66, 82, 66 };
            foreach (var w in colWidths) this._paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, w));
            this._paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this._paramsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));

            this._presetCombo.Dock = DockStyle.Fill;
            this._presetCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._presetCombo.Items.AddRange(new object[] { "快速", "标准", "精细", "自定义" });
            this._presetCombo.SelectedIndex = 1;

            this._cvFoldsSpinner.Dock = DockStyle.Fill;
            this._cvFoldsSpinner.Minimum = 0; this._cvFoldsSpinner.Maximum = 10; this._cvFoldsSpinner.Value = 5;

            this._testFractionSpinner.Dock = DockStyle.Fill;
            this._testFractionSpinner.Minimum = 0.1m; this._testFractionSpinner.Maximum = 0.5m; this._testFractionSpinner.Value = 0.2m; this._testFractionSpinner.DecimalPlaces = 1; this._testFractionSpinner.Increment = 0.1m;

            this._augmentCheckbox.Text = "数据增强"; this._augmentCheckbox.AutoSize = true;

            this._colEpochsSpinner.Dock = DockStyle.Fill; this._colEpochsSpinner.Minimum = 1; this._colEpochsSpinner.Maximum = 50; this._colEpochsSpinner.Value = 4;
            this._colBatchSpinner.Dock = DockStyle.Fill; this._colBatchSpinner.Minimum = 4; this._colBatchSpinner.Maximum = 128; this._colBatchSpinner.Value = 32;
            this._nerEpochsSpinner.Dock = DockStyle.Fill; this._nerEpochsSpinner.Minimum = 1; this._nerEpochsSpinner.Maximum = 50; this._nerEpochsSpinner.Value = 4;
            this._nerBatchSpinner.Dock = DockStyle.Fill; this._nerBatchSpinner.Minimum = 4; this._nerBatchSpinner.Maximum = 128; this._nerBatchSpinner.Value = 32;
            this._secTreesSpinner.Dock = DockStyle.Fill; this._secTreesSpinner.Minimum = 10; this._secTreesSpinner.Maximum = 1000; this._secTreesSpinner.Value = 200;
            this._secLeavesSpinner.Dock = DockStyle.Fill; this._secLeavesSpinner.Minimum = 5; this._secLeavesSpinner.Maximum = 100; this._secLeavesSpinner.Value = 20;
            this._secMinLeafSpinner.Dock = DockStyle.Fill; this._secMinLeafSpinner.Minimum = 1; this._secMinLeafSpinner.Maximum = 20; this._secMinLeafSpinner.Value = 2;

            // Row 0
            this._paramsLayout.Controls.Add(new Label { Text = "预设:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            this._paramsLayout.Controls.Add(this._presetCombo, 1, 0);
            this._paramsLayout.Controls.Add(new Label { Text = "CV折数:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 0);
            this._paramsLayout.Controls.Add(this._cvFoldsSpinner, 3, 0);
            this._paramsLayout.Controls.Add(new Label { Text = "测试集:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 4, 0);
            this._paramsLayout.Controls.Add(this._testFractionSpinner, 5, 0);
            this._paramsLayout.Controls.Add(this._augmentCheckbox, 6, 0);
            // Row 1
            this._paramsLayout.Controls.Add(new Label { Text = "列名 Epoch:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
            this._paramsLayout.Controls.Add(this._colEpochsSpinner, 1, 1);
            this._paramsLayout.Controls.Add(new Label { Text = "列名 Batch:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 1);
            this._paramsLayout.Controls.Add(this._colBatchSpinner, 3, 1);
            this._paramsLayout.Controls.Add(new Label { Text = "NER Epoch:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 4, 1);
            this._paramsLayout.Controls.Add(this._nerEpochsSpinner, 5, 1);
            this._paramsLayout.Controls.Add(new Label { Text = "NER Batch:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 6, 1);
            this._paramsLayout.Controls.Add(this._nerBatchSpinner, 7, 1);
            // Row 2
            this._paramsLayout.Controls.Add(new Label { Text = "章节树数:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 2);
            this._paramsLayout.Controls.Add(this._secTreesSpinner, 1, 2);
            this._paramsLayout.Controls.Add(new Label { Text = "章节叶:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 2);
            this._paramsLayout.Controls.Add(this._secLeavesSpinner, 3, 2);
            this._paramsLayout.Controls.Add(new Label { Text = "最小叶样本:", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 4, 2);
            this._paramsLayout.Controls.Add(this._secMinLeafSpinner, 5, 2);

            this._paramsGroup.Controls.Add(this._paramsLayout);

            // ── Train Buttons ─────────────────────────────────────────────────
            this._trainBtnFlow.Dock = DockStyle.Fill;
            this._trainBtnFlow.Padding = new Padding(0, 6, 0, 0);

            this._trainUnifiedBtn.Text = "开始训练";
            this._trainUnifiedBtn.Type = AntdUI.TTypeMini.Primary;
            this._trainUnifiedBtn.Size = new Size(150, 36);
            this._trainUnifiedBtn.Font = new Font("微软雅黑", 9, FontStyle.Bold);

            this._cancelTrainBtn.Text = "取消训练";
            this._cancelTrainBtn.Type = AntdUI.TTypeMini.Error;
            this._cancelTrainBtn.Size = new Size(100, 36);
            this._cancelTrainBtn.Enabled = false;

            this._trainBtnFlow.Controls.AddRange(new Control[] { this._trainUnifiedBtn, this._cancelTrainBtn });

            // ── Eval Panel ────────────────────────────────────────────────────
            this._evalPanel.Dock = DockStyle.Fill;
            this._evalPanel.RowCount = 2;
            this._evalPanel.ColumnCount = 1;
            this._evalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            this._evalPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            this._evalLabel.Text = "请先添加训练数据并点击训练按钮";
            this._evalLabel.Dock = DockStyle.Fill;
            this._evalLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._evalLabel.Font = new Font("微软雅黑", 9);
            this._evalLabel.AutoSize = false;

            this._evalCompareLabel.Text = "";
            this._evalCompareLabel.Dock = DockStyle.Fill;
            this._evalCompareLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._evalCompareLabel.Font = new Font("微软雅黑", 8.5f);
            this._evalCompareLabel.ForeColor = Color.FromArgb(100, 100, 100);

            this._evalPanel.Controls.Add(this._evalLabel, 0, 0);
            this._evalPanel.Controls.Add(this._evalCompareLabel, 0, 1);

            // ── Train Log ─────────────────────────────────────────────────────
            this._trainProgressBar.Dock = DockStyle.Top;
            this._trainProgressBar.Height = 6;

            this._trainLogBox.Dock = DockStyle.Fill;
            this._trainLogBox.ReadOnly = true;
            this._trainLogBox.BackColor = Color.FromArgb(30, 30, 30);
            this._trainLogBox.ForeColor = Color.FromArgb(204, 204, 204);
            this._trainLogBox.Font = new Font("Consolas", 9);

            this._trainLogPanel.Dock = DockStyle.Fill;
            this._trainLogPanel.Controls.Add(this._trainLogBox);
            this._trainLogPanel.Controls.Add(this._trainProgressBar);

            // ── Assemble ──────────────────────────────────────────────────────
            this._layout.Controls.Add(this._statsGroup, 0, 0);
            this._layout.Controls.Add(this._paramsGroup, 0, 1);
            this._layout.Controls.Add(this._trainBtnFlow, 0, 2);
            this._layout.Controls.Add(this._evalPanel, 0, 3);
            this._layout.Controls.Add(this._trainLogPanel, 0, 4);

            this.Controls.Add(this._layout);

            foreach (var s in new System.Windows.Forms.NumericUpDown[] { _cvFoldsSpinner, _testFractionSpinner, _colEpochsSpinner, _colBatchSpinner, _nerEpochsSpinner, _nerBatchSpinner, _secTreesSpinner, _secLeavesSpinner, _secMinLeafSpinner })
                ((System.ComponentModel.ISupportInitialize)s).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("微软雅黑", 9F);
            this.Name = "TrainingPanel";
            this.Size = new Size(900, 700);
            this.Padding = new Padding(8);

            this.ResumeLayout(false);
        }

        private TableLayoutPanel _layout;
        private GroupBox _statsGroup;
        private FlowLayoutPanel _statsFlow;
        private Label _colSampleCountLabel;
        private Label _nerSampleCountLabel;
        private Label _sectionSampleCountLabel;
        private Label _knowledgeCountLabel;
        private Label _modelHealthLabel;
        private AntdUI.Button _genFromKnowledgeBtn;
        private AntdUI.Button _importCsvBtn;
        private AntdUI.Button _importSectionWordBtn;
        private AntdUI.Button _columnErrorAnalysisBtn;
        private GroupBox _paramsGroup;
        private TableLayoutPanel _paramsLayout;
        private ComboBox _presetCombo;
        private NumericUpDown _cvFoldsSpinner;
        private NumericUpDown _testFractionSpinner;
        private CheckBox _augmentCheckbox;
        private NumericUpDown _colEpochsSpinner;
        private NumericUpDown _colBatchSpinner;
        private NumericUpDown _nerEpochsSpinner;
        private NumericUpDown _nerBatchSpinner;
        private NumericUpDown _secTreesSpinner;
        private NumericUpDown _secLeavesSpinner;
        private NumericUpDown _secMinLeafSpinner;
        private FlowLayoutPanel _trainBtnFlow;
        private AntdUI.Button _trainUnifiedBtn;
        private AntdUI.Button _cancelTrainBtn;
        private TableLayoutPanel _evalPanel;
        private Label _evalLabel;
        private Label _evalCompareLabel;
        private Panel _trainLogPanel;
        private ProgressBar _trainProgressBar;
        private RichTextBox _trainLogBox;
    }
}
