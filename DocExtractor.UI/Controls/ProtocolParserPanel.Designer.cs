using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    partial class ProtocolParserPanel
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
            this._fileGroup = new GroupBox();
            this._fileLayout = new FlowLayoutPanel();
            this._filePathLabel = new Label();
            this._templateTypeCombo = new ComboBox();
            this._browseBtn = new AntdUI.Button();
            this._analyzeBtn = new AntdUI.Button();
            this._settingsGroup = new GroupBox();
            this._settingsLayout = new TableLayoutPanel();
            this._systemNameInput = new AntdUI.Input();
            this._codePrefixInput = new AntdUI.Input();
            this._formulaTypeInput = new AntdUI.Input();
            this._formulaCoeffInput = new AntdUI.Input();
            this._exportFormatCombo = new ComboBox();
            this._includeHeaderCheck = new CheckBox();
            this._includeChecksumCheck = new CheckBox();
            this._previewGroup = new GroupBox();
            this._previewBox = new RichTextBox();
            this._actionFlow = new FlowLayoutPanel();
            this._exportBtn = new AntdUI.Button();
            this._downloadTemplateBtn = new AntdUI.Button();
            this._openFolderBtn = new AntdUI.Button();
            this._resultGroup = new GroupBox();
            this._resultBox = new RichTextBox();

            this.SuspendLayout();

            // ── Main Layout ───────────────────────────────────────────────────
            this._layout.Dock = DockStyle.Fill;
            this._layout.RowCount = 5;
            this._layout.ColumnCount = 1;
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
            this._layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // ── File Selection ────────────────────────────────────────────────
            this._fileGroup.Text = "协议文档";
            this._fileGroup.Dock = DockStyle.Fill;

            this._fileLayout.Dock = DockStyle.Fill;
            this._fileLayout.Padding = new Padding(8, 12, 8, 4);

            this._filePathLabel.Text = "请选择协议文档 (.docx)";
            this._filePathLabel.Size = new Size(500, 28);
            this._filePathLabel.TextAlign = ContentAlignment.MiddleLeft;
            this._filePathLabel.AutoEllipsis = true;
            this._filePathLabel.ForeColor = Color.Gray;

            this._templateTypeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._templateTypeCombo.Items.AddRange(new object[] { "遥测模板", "遥控模板" });
            this._templateTypeCombo.SelectedIndex = 0;
            this._templateTypeCombo.Width = 100;

            this._browseBtn.Text = "选择文件";
            this._browseBtn.Size = new Size(100, 32);

            this._analyzeBtn.Text = "分析协议";
            this._analyzeBtn.Type = AntdUI.TTypeMini.Primary;
            this._analyzeBtn.Size = new Size(100, 32);
            this._analyzeBtn.Enabled = false;

            this._fileLayout.Controls.AddRange(new Control[]
            {
                this._filePathLabel,
                new Label { Text = "模板:", Width = 50, TextAlign = ContentAlignment.MiddleRight },
                this._templateTypeCombo,
                this._browseBtn,
                this._analyzeBtn
            });
            this._fileGroup.Controls.Add(this._fileLayout);

            // ── Settings ──────────────────────────────────────────────────────
            this._settingsGroup.Text = "导出设置";
            this._settingsGroup.Dock = DockStyle.Fill;

            this._settingsLayout.Dock = DockStyle.Fill;
            this._settingsLayout.Padding = new Padding(4);
            this._settingsLayout.ColumnCount = 8;
            this._settingsLayout.RowCount = 2;
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            this._settingsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            this._settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            this._settingsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            this._systemNameInput.PlaceholderText = "自动检测";
            this._systemNameInput.Dock = DockStyle.Fill;

            this._codePrefixInput.PlaceholderText = "同系统名";
            this._codePrefixInput.Dock = DockStyle.Fill;

            this._formulaTypeInput.Text = "5";
            this._formulaTypeInput.Dock = DockStyle.Fill;

            this._formulaCoeffInput.Text = "1/0/";
            this._formulaCoeffInput.Dock = DockStyle.Fill;

            this._exportFormatCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._exportFormatCombo.Items.AddRange(new object[] { "格式A+B", "仅格式A", "仅格式B" });
            this._exportFormatCombo.SelectedIndex = 0;
            this._exportFormatCombo.Dock = DockStyle.Fill;

            this._includeHeaderCheck.Text = "含报头字段";
            this._includeHeaderCheck.AutoSize = true;
            this._includeHeaderCheck.Dock = DockStyle.Fill;

            this._includeChecksumCheck.Text = "含校验和";
            this._includeChecksumCheck.AutoSize = true;
            this._includeChecksumCheck.Checked = true;
            this._includeChecksumCheck.Dock = DockStyle.Fill;

            // Row 0
            this._settingsLayout.Controls.Add(
                new Label { Text = "系统名:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            this._settingsLayout.Controls.Add(this._systemNameInput, 1, 0);
            this._settingsLayout.Controls.Add(
                new Label { Text = "代号前缀:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 2, 0);
            this._settingsLayout.Controls.Add(this._codePrefixInput, 3, 0);
            this._settingsLayout.Controls.Add(
                new Label { Text = "公式类型:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 4, 0);
            this._settingsLayout.Controls.Add(this._formulaTypeInput, 5, 0);
            this._settingsLayout.Controls.Add(
                new Label { Text = "公式系数:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 6, 0);
            this._settingsLayout.Controls.Add(this._formulaCoeffInput, 7, 0);

            // Row 1
            this._settingsLayout.Controls.Add(this._includeHeaderCheck, 1, 1);
            this._settingsLayout.Controls.Add(this._includeChecksumCheck, 3, 1);
            this._settingsLayout.Controls.Add(
                new Label { Text = "导出格式:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 4, 1);
            this._settingsLayout.Controls.Add(this._exportFormatCombo, 5, 1);

            this._settingsGroup.Controls.Add(this._settingsLayout);

            // ── Preview ───────────────────────────────────────────────────────
            this._previewGroup.Text = "分析结果预览";
            this._previewGroup.Dock = DockStyle.Fill;

            this._previewBox.Dock = DockStyle.Fill;
            this._previewBox.ReadOnly = true;
            this._previewBox.BackColor = Color.FromArgb(250, 250, 250);
            this._previewBox.Font = new Font("Consolas", 9);
            this._previewBox.BorderStyle = BorderStyle.None;
            this._previewGroup.Controls.Add(this._previewBox);

            // ── Action Buttons ────────────────────────────────────────────────
            this._actionFlow.Dock = DockStyle.Fill;
            this._actionFlow.Padding = new Padding(0, 6, 0, 0);

            this._exportBtn.Text = "生成 Excel 配置";
            this._exportBtn.Type = AntdUI.TTypeMini.Primary;
            this._exportBtn.Size = new Size(160, 36);
            this._exportBtn.Font = new Font("微软雅黑", 9, FontStyle.Bold);
            this._exportBtn.Enabled = false;

            this._downloadTemplateBtn.Text = "下载空白模板";
            this._downloadTemplateBtn.Size = new Size(130, 36);

            this._openFolderBtn.Text = "打开输出目录";
            this._openFolderBtn.Size = new Size(130, 36);
            this._openFolderBtn.Enabled = false;

            this._actionFlow.Controls.AddRange(new Control[]
            {
                this._exportBtn, this._downloadTemplateBtn, this._openFolderBtn
            });

            // ── Result Log ────────────────────────────────────────────────────
            this._resultGroup.Text = "导出结果";
            this._resultGroup.Dock = DockStyle.Fill;

            this._resultBox.Dock = DockStyle.Fill;
            this._resultBox.ReadOnly = true;
            this._resultBox.BackColor = Color.FromArgb(30, 30, 30);
            this._resultBox.ForeColor = Color.FromArgb(204, 204, 204);
            this._resultBox.Font = new Font("Consolas", 9);
            this._resultBox.BorderStyle = BorderStyle.None;
            this._resultGroup.Controls.Add(this._resultBox);

            // ── Assemble ──────────────────────────────────────────────────────
            this._layout.Controls.Add(this._fileGroup, 0, 0);
            this._layout.Controls.Add(this._settingsGroup, 0, 1);
            this._layout.Controls.Add(this._previewGroup, 0, 2);
            this._layout.Controls.Add(this._actionFlow, 0, 3);
            this._layout.Controls.Add(this._resultGroup, 0, 4);

            this.Controls.Add(this._layout);

            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Font = new Font("微软雅黑", 9F);
            this.Name = "ProtocolParserPanel";
            this.Size = new Size(900, 700);
            this.Padding = new Padding(8);

            this.ResumeLayout(false);
        }

        private TableLayoutPanel _layout;
        private GroupBox _fileGroup;
        private FlowLayoutPanel _fileLayout;
        private Label _filePathLabel;
        private ComboBox _templateTypeCombo;
        private AntdUI.Button _browseBtn;
        private AntdUI.Button _analyzeBtn;
        private GroupBox _settingsGroup;
        private TableLayoutPanel _settingsLayout;
        private AntdUI.Input _systemNameInput;
        private AntdUI.Input _codePrefixInput;
        private AntdUI.Input _formulaTypeInput;
        private AntdUI.Input _formulaCoeffInput;
        private ComboBox _exportFormatCombo;
        private CheckBox _includeHeaderCheck;
        private CheckBox _includeChecksumCheck;
        private GroupBox _previewGroup;
        private RichTextBox _previewBox;
        private FlowLayoutPanel _actionFlow;
        private AntdUI.Button _exportBtn;
        private AntdUI.Button _downloadTemplateBtn;
        private AntdUI.Button _openFolderBtn;
        private GroupBox _resultGroup;
        private RichTextBox _resultBox;
    }
}
