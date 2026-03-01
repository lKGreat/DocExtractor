using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class MainForm
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
            this._menuStrip = new MenuStrip();
            this._toolbar = new Panel();
            this._navPanel = new Panel();
            this._navExtractionBtn = new AntdUI.Button();
            this._navFieldConfigBtn = new AntdUI.Button();
            this._navSplitRuleBtn = new AntdUI.Button();
            this._navTrainingBtn = new AntdUI.Button();
            this._navProtocolBtn = new AntdUI.Button();
            this._contentPanel = new Panel();
            this._statusStrip = new StatusStrip();
            this._statusBarLabel = new ToolStripStatusLabel();
            this._configCombo = new ComboBox();

            this.SuspendLayout();

            // ── Window ────────────────────────────────────────────────────────
            this.Text = "DocExtractor \u2014 文档数据智能抽取系统";
            this.Size = new Size(1400, 900);
            this.MinimumSize = new Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("微软雅黑", 9F);
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.Name = "MainForm";

            // ── Menu Strip ────────────────────────────────────────────────────
            this._menuStrip.Dock = DockStyle.Top;

            var fileMenu = new ToolStripMenuItem("文件(&F)");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("添加文件...", null, (s, e) => OnMenuAddFiles(s, e)) { ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O });
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("导出结果...", null, (s, e) => OnMenuExport(s, e)) { ShortcutKeys = System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S });
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("打开模板目录", null, (s, e) => OnOpenTemplateDir()));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("退出", null, (s, e) => Close()));

            var toolMenu = new ToolStripMenuItem("工具(&T)");
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("重新生成模板", null, (s, e) => OnRegenerateTemplates()));
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("重新加载模型", null, (s, e) => OnReloadModels()));
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("模型版本管理", null, (s, e) => OnOpenModelManager()));
            toolMenu.DropDownItems.Add(new ToolStripSeparator());
            toolMenu.DropDownItems.Add(new ToolStripMenuItem("配置包管理器", null, (s, e) => OnOpenPackManager()));

            var diagnoseMenu = new ToolStripMenuItem("诊断(&D)");
            diagnoseMenu.DropDownItems.Add(new ToolStripMenuItem("系统健康度报告", null, (s, e) => OnOpenDiagnostics()));

            var helpMenu = new ToolStripMenuItem("帮助(&H)");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("关于", null, (s, e) =>
                Helpers.MessageHelper.Info(this, "DocExtractor v1.0 \u2014 文档数据智能抽取系统")));

            this._menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, toolMenu, diagnoseMenu, helpMenu });

            // ── Toolbar ───────────────────────────────────────────────────────
            this._toolbar.Dock = DockStyle.Top;
            this._toolbar.Height = 46;
            this._toolbar.Padding = new Padding(8, 6, 8, 6);

            var toolFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var configLabel = new Label { Text = "配置：", TextAlign = System.Drawing.ContentAlignment.MiddleLeft, Width = 45, Height = 34 };

            this._configCombo.Width = 200;
            this._configCombo.Height = 32;
            this._configCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            this._configCombo.Font = new Font("微软雅黑", 9F);

            toolFlow.Controls.AddRange(new Control[] { configLabel, this._configCombo });
            this._toolbar.Controls.Add(toolFlow);

            // ── Nav Panel (left sidebar) ──────────────────────────────────────
            this._navPanel.Dock = DockStyle.Left;
            this._navPanel.Width = 110;
            this._navPanel.Padding = new Padding(6, 8, 6, 8);
            this._navPanel.BackColor = Color.FromArgb(240, 242, 245);

            this._navExtractionBtn.Text = "数据抽取";
            this._navExtractionBtn.Type = AntdUI.TTypeMini.Primary;
            this._navExtractionBtn.Dock = DockStyle.Top;
            this._navExtractionBtn.Height = 46;
            this._navExtractionBtn.Margin = new Padding(0, 0, 0, 4);
            this._navExtractionBtn.Click += OnNavExtraction;

            this._navFieldConfigBtn.Text = "字段配置";
            this._navFieldConfigBtn.Type = AntdUI.TTypeMini.Default;
            this._navFieldConfigBtn.Dock = DockStyle.Top;
            this._navFieldConfigBtn.Height = 46;
            this._navFieldConfigBtn.Margin = new Padding(0, 0, 0, 4);
            this._navFieldConfigBtn.Click += OnNavFieldConfig;

            this._navSplitRuleBtn.Text = "拆分规则";
            this._navSplitRuleBtn.Type = AntdUI.TTypeMini.Default;
            this._navSplitRuleBtn.Dock = DockStyle.Top;
            this._navSplitRuleBtn.Height = 46;
            this._navSplitRuleBtn.Margin = new Padding(0, 0, 0, 4);
            this._navSplitRuleBtn.Click += OnNavSplitRule;

            this._navTrainingBtn.Text = "模型训练";
            this._navTrainingBtn.Type = AntdUI.TTypeMini.Default;
            this._navTrainingBtn.Dock = DockStyle.Top;
            this._navTrainingBtn.Height = 46;
            this._navTrainingBtn.Margin = new Padding(0, 0, 0, 4);
            this._navTrainingBtn.Click += OnNavTraining;

            this._navProtocolBtn.Text = "协议解析";
            this._navProtocolBtn.Type = AntdUI.TTypeMini.Default;
            this._navProtocolBtn.Dock = DockStyle.Top;
            this._navProtocolBtn.Height = 46;
            this._navProtocolBtn.Margin = new Padding(0, 0, 0, 4);
            this._navProtocolBtn.Click += OnNavProtocol;

            // Add in reverse order because DockStyle.Top stacks bottom-up
            this._navPanel.Controls.Add(this._navProtocolBtn);
            this._navPanel.Controls.Add(this._navTrainingBtn);
            this._navPanel.Controls.Add(this._navSplitRuleBtn);
            this._navPanel.Controls.Add(this._navFieldConfigBtn);
            this._navPanel.Controls.Add(this._navExtractionBtn);

            // ── Content Panel ─────────────────────────────────────────────────
            this._contentPanel.Dock = DockStyle.Fill;

            // ── Status Bar ────────────────────────────────────────────────────
            this._statusBarLabel.Text = "就绪";
            this._statusBarLabel.Spring = true;
            this._statusBarLabel.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this._statusStrip.Items.Add(this._statusBarLabel);

            // ── Assemble ──────────────────────────────────────────────────────
            this.Controls.Add(this._contentPanel);
            this.Controls.Add(this._navPanel);
            this.Controls.Add(this._toolbar);
            this.Controls.Add(this._menuStrip);
            this.Controls.Add(this._statusStrip);
            this.MainMenuStrip = this._menuStrip;

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private MenuStrip _menuStrip;
        private Panel _toolbar;
        private ComboBox _configCombo;
        private Panel _navPanel;
        private AntdUI.Button _navExtractionBtn;
        private AntdUI.Button _navFieldConfigBtn;
        private AntdUI.Button _navSplitRuleBtn;
        private AntdUI.Button _navTrainingBtn;
        private AntdUI.Button _navProtocolBtn;
        private Panel _contentPanel;
        private StatusStrip _statusStrip;
        private ToolStripStatusLabel _statusBarLabel;
    }
}
