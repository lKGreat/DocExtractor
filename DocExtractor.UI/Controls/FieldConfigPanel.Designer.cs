using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    partial class FieldConfigPanel
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
            this._topBar = new Panel();
            this._topFlow = new FlowLayoutPanel();
            this._configTypeLabel = new Label();
            this._importConfigBtn = new AntdUI.Button();
            this._exportConfigBtn = new AntdUI.Button();
            this._configSplit = new SplitContainer();
            this._fieldsGrid = new DataGridView();
            this._settingsPanel = new TableLayoutPanel();
            this._headerRowsSpinner = new NumericUpDown();
            this._columnMatchCombo = new ComboBox();
            this._valueNormalizationCheckBox = new CheckBox();
            this._valueCleaningCheckBox = new CheckBox();
            this._cleaningRulesBtn = new AntdUI.Button();
            this._btnBar = new FlowLayoutPanel();
            this._saveConfigBtn = new AntdUI.Button();
            this._setDefaultBtn = new AntdUI.Button();
            this._newConfigBtn = new AntdUI.Button();
            this._deleteConfigBtn = new AntdUI.Button();

            this.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this._configSplit).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._fieldsGrid).BeginInit();
            ((System.ComponentModel.ISupportInitialize)this._headerRowsSpinner).BeginInit();

            // ── Top Bar ───────────────────────────────────────────────────────
            this._topBar.Dock = DockStyle.Top;
            this._topBar.Height = 48;
            this._topBar.Padding = new Padding(4);

            this._topFlow.Dock = DockStyle.Fill;
            this._topFlow.FlowDirection = FlowDirection.LeftToRight;
            this._topFlow.WrapContents = false;

            this._configTypeLabel.Text = "内置配置";
            this._configTypeLabel.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._configTypeLabel.ForeColor = Color.White;
            this._configTypeLabel.BackColor = Color.FromArgb(22, 119, 255);
            this._configTypeLabel.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this._configTypeLabel.AutoSize = false;
            this._configTypeLabel.Size = new Size(80, 28);
            this._configTypeLabel.Margin = new Padding(0, 6, 12, 0);

            this._importConfigBtn.Text = "从 Excel 导入";
            this._importConfigBtn.Size = new Size(120, 32);
            this._importConfigBtn.Margin = new Padding(0, 0, 4, 0);

            this._exportConfigBtn.Text = "导出为 Excel";
            this._exportConfigBtn.Size = new Size(110, 32);
            this._exportConfigBtn.Margin = new Padding(0, 0, 4, 0);

            this._importJsonBtn = new AntdUI.Button();
            this._importJsonBtn.Text = "从 JSON 导入";
            this._importJsonBtn.Size = new Size(115, 32);
            this._importJsonBtn.Margin = new Padding(0, 0, 4, 0);

            this._exportJsonBtn = new AntdUI.Button();
            this._exportJsonBtn.Text = "导出为 JSON";
            this._exportJsonBtn.Size = new Size(110, 32);

            this._topFlow.Controls.AddRange(new Control[] {
                this._configTypeLabel,
                this._importConfigBtn, this._exportConfigBtn,
                this._importJsonBtn, this._exportJsonBtn
            });
            this._topBar.Controls.Add(this._topFlow);

            // ── Config Splitter (top: fields grid | bottom: global settings) ──
            this._configSplit.Dock = DockStyle.Fill;
            this._configSplit.Orientation = Orientation.Horizontal;
            this._configSplit.SplitterDistance = 400;

            // ── Fields Grid ───────────────────────────────────────────────────
            this._fieldsGrid.Dock = DockStyle.Fill;
            this._fieldsGrid.AllowUserToAddRows = true;
            this._fieldsGrid.AllowUserToDeleteRows = true;
            this._fieldsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            this._fieldsGrid.Font = new Font("微软雅黑", 9);
            this._fieldsGrid.Columns.AddRange(new DataGridViewColumn[]
            {
                new DataGridViewTextBoxColumn { Name = "FieldName", HeaderText = "字段名（英文）", FillWeight = 15 },
                new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "显示名（中文）", FillWeight = 15 },
                new DataGridViewComboBoxColumn
                {
                    Name = "DataType", HeaderText = "数据类型",
                    DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.FieldDataType)),
                    FillWeight = 12
                },
                new DataGridViewCheckBoxColumn { Name = "IsRequired", HeaderText = "必填", FillWeight = 6 },
                new DataGridViewTextBoxColumn { Name = "DefaultValue", HeaderText = "默认值", FillWeight = 12 },
                new DataGridViewTextBoxColumn { Name = "Variants", HeaderText = "列名变体（逗号分隔）", FillWeight = 35 },
                new DataGridViewButtonColumn { Name = "EditField", HeaderText = "编辑", Text = "编辑", UseColumnTextForButtonValue = true, FillWeight = 8 }
            });
            this._configSplit.Panel1.Controls.Add(this._fieldsGrid);

            // ── Global Settings ───────────────────────────────────────────────
            this._timeAxisCheckBox = new CheckBox();
            this._timeAxisToleranceLabel = new Label();
            this._timeAxisToleranceSpinner = new NumericUpDown();

            ((System.ComponentModel.ISupportInitialize)this._timeAxisToleranceSpinner).BeginInit();

            this._settingsPanel.Dock = DockStyle.Fill;
            this._settingsPanel.ColumnCount = 4;
            this._settingsPanel.RowCount = 4;
            this._settingsPanel.Padding = new Padding(8);
            this._settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            this._settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            this._settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            this._settingsPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            this._headerRowsSpinner.Minimum = 1;
            this._headerRowsSpinner.Maximum = 5;
            this._headerRowsSpinner.Value = 1;
            this._headerRowsSpinner.Width = 80;

            this._columnMatchCombo.DataSource = System.Enum.GetNames(typeof(DocExtractor.Core.Models.ColumnMatchMode));
            this._columnMatchCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._columnMatchCombo.Width = 180;

            this._valueNormalizationCheckBox.Text = "启用抽取值归一化（推荐）";
            this._valueNormalizationCheckBox.Checked = true;
            this._valueNormalizationCheckBox.AutoSize = true;

            this._valueCleaningCheckBox.Text = "启用值清洗（去除单位/注释/描述）";
            this._valueCleaningCheckBox.Checked = false;
            this._valueCleaningCheckBox.AutoSize = true;

            this._cleaningRulesBtn.Text = "配置清洗规则...";
            this._cleaningRulesBtn.Size = new Size(130, 28);
            this._cleaningRulesBtn.Enabled = false;

            this._settingsPanel.Controls.Add(new Label { Text = "表头行数：", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Width = 80 }, 0, 0);
            this._settingsPanel.Controls.Add(this._headerRowsSpinner, 1, 0);
            this._settingsPanel.Controls.Add(new Label { Text = "列名匹配：", TextAlign = System.Drawing.ContentAlignment.MiddleRight, Width = 80 }, 2, 0);
            this._settingsPanel.Controls.Add(this._columnMatchCombo, 3, 0);
            this._settingsPanel.Controls.Add(this._valueNormalizationCheckBox, 0, 1);
            this._settingsPanel.SetColumnSpan(this._valueNormalizationCheckBox, 4);
            this._settingsPanel.Controls.Add(this._valueCleaningCheckBox, 0, 2);
            this._settingsPanel.SetColumnSpan(this._valueCleaningCheckBox, 2);
            this._settingsPanel.Controls.Add(this._cleaningRulesBtn, 2, 2);

            this._timeAxisCheckBox.Text = "启用时间轴展开（自动检测多步/跳变/阈值模式）";
            this._timeAxisCheckBox.Checked = false;
            this._timeAxisCheckBox.AutoSize = true;

            this._timeAxisToleranceLabel.Text = "公差 ±";
            this._timeAxisToleranceLabel.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this._timeAxisToleranceLabel.Width = 50;

            this._timeAxisToleranceSpinner.DecimalPlaces = 2;
            this._timeAxisToleranceSpinner.Increment = 0.1M;
            this._timeAxisToleranceSpinner.Maximum = 100;
            this._timeAxisToleranceSpinner.Minimum = 0;
            this._timeAxisToleranceSpinner.Value = 0;
            this._timeAxisToleranceSpinner.Width = 80;
            this._timeAxisToleranceSpinner.Enabled = false;

            this._settingsPanel.Controls.Add(this._timeAxisCheckBox, 0, 3);
            this._settingsPanel.SetColumnSpan(this._timeAxisCheckBox, 2);
            this._settingsPanel.Controls.Add(this._timeAxisToleranceLabel, 2, 3);
            this._settingsPanel.Controls.Add(this._timeAxisToleranceSpinner, 3, 3);

            this._configSplit.Panel2.Controls.Add(this._settingsPanel);

            // ── Button Bar ────────────────────────────────────────────────────
            this._btnBar.Dock = DockStyle.Bottom;
            this._btnBar.Height = 42;

            this._saveConfigBtn.Text = "保存配置";
            this._saveConfigBtn.Type = AntdUI.TTypeMini.Primary;
            this._saveConfigBtn.Size = new Size(100, 34);

            this._setDefaultBtn.Text = "设为默认";
            this._setDefaultBtn.Size = new Size(100, 34);

            this._newConfigBtn.Text = "新建配置";
            this._newConfigBtn.Size = new Size(100, 34);

            this._deleteConfigBtn.Text = "删除配置";
            this._deleteConfigBtn.Type = AntdUI.TTypeMini.Error;
            this._deleteConfigBtn.Size = new Size(100, 34);

            this._btnBar.Controls.AddRange(new Control[] { this._saveConfigBtn, this._setDefaultBtn, this._newConfigBtn, this._deleteConfigBtn });

            this.Controls.Add(this._configSplit);
            this.Controls.Add(this._topBar);
            this.Controls.Add(this._btnBar);

            ((System.ComponentModel.ISupportInitialize)this._configSplit).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._fieldsGrid).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._headerRowsSpinner).EndInit();
            ((System.ComponentModel.ISupportInitialize)this._timeAxisToleranceSpinner).EndInit();

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("微软雅黑", 9F);
            this.Name = "FieldConfigPanel";
            this.Size = new Size(900, 600);
            this.Padding = new Padding(8);

            this.ResumeLayout(false);
        }

        private Panel _topBar;
        private FlowLayoutPanel _topFlow;
        private Label _configTypeLabel;
        private AntdUI.Button _importConfigBtn;
        private AntdUI.Button _exportConfigBtn;
        private AntdUI.Button _importJsonBtn;
        private AntdUI.Button _exportJsonBtn;
        private SplitContainer _configSplit;
        private DataGridView _fieldsGrid;
        private TableLayoutPanel _settingsPanel;
        private NumericUpDown _headerRowsSpinner;
        private ComboBox _columnMatchCombo;
        private CheckBox _valueNormalizationCheckBox;
        private CheckBox _valueCleaningCheckBox;
        private AntdUI.Button _cleaningRulesBtn;
        private FlowLayoutPanel _btnBar;
        private AntdUI.Button _saveConfigBtn;
        private AntdUI.Button _setDefaultBtn;
        private AntdUI.Button _newConfigBtn;
        private AntdUI.Button _deleteConfigBtn;
        private CheckBox _timeAxisCheckBox;
        private Label _timeAxisToleranceLabel;
        private NumericUpDown _timeAxisToleranceSpinner;
    }
}
