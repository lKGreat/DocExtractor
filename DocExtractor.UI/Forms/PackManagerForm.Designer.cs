using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class PackManagerForm
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
            components = new System.ComponentModel.Container();
            SuspendLayout();

            Text = "配置包管理器";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            var mainSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 260
            };

            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var titleLabel = new Label
            {
                Text = "已安装配置包",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("微软雅黑", 10F, FontStyle.Bold)
            };
            _installedListBox = new ListBox { Dock = DockStyle.Fill };

            var leftBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 42
            };
            _refreshBtn = new AntdUI.Button { Text = "刷新", Size = new Size(80, 32) };
            leftBtnPanel.Controls.Add(_refreshBtn);

            leftPanel.Controls.Add(_installedListBox);
            leftPanel.Controls.Add(leftBtnPanel);
            leftPanel.Controls.Add(titleLabel);

            var rightPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            var actionLabel = new Label
            {
                Text = "操作",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("微软雅黑", 10F, FontStyle.Bold)
            };
            var actionsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 48
            };
            _installBtn = new AntdUI.Button
            {
                Text = "从文件安装 (.dxpack)",
                Type = AntdUI.TTypeMini.Primary,
                Size = new Size(180, 34)
            };
            _exportBtn = new AntdUI.Button
            {
                Text = "导出当前配置为包",
                Size = new Size(160, 34)
            };
            actionsPanel.Controls.Add(_installBtn);
            actionsPanel.Controls.Add(_exportBtn);

            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(204, 204, 204),
                Font = new Font("Consolas", 9),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            rightPanel.Controls.Add(_logBox);
            rightPanel.Controls.Add(actionsPanel);
            rightPanel.Controls.Add(actionLabel);

            mainSplit.Panel1.Controls.Add(leftPanel);
            mainSplit.Panel2.Controls.Add(rightPanel);

            Controls.Add(mainSplit);
            ResumeLayout(false);
        }

        private ListBox _installedListBox;
        private AntdUI.Button _refreshBtn;
        private AntdUI.Button _installBtn;
        private AntdUI.Button _exportBtn;
        private RichTextBox _logBox;
    }
}
