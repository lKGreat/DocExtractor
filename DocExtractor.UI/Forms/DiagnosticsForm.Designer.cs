using System.Drawing;
using System.Windows.Forms;

namespace DocExtractor.UI.Forms
{
    partial class DiagnosticsForm
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

            Text = "系统诊断";
            Size = new Size(760, 520);
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("微软雅黑", 9F);

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            _generatedAtLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "生成时间：--" };
            _columnAccuracyLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "列名识别准确率：--" };
            _testCoverageLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "测试覆盖率：--" };
            _knowledgeLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "知识库规模：--" };
            _sampleLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "样本：--" };
            _modelStatusLabel = new Label { Dock = DockStyle.Top, Height = 28, Text = "模型状态：--" };

            var suggestionsLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 30,
                Text = "建议行动：",
                Font = new Font("微软雅黑", 10F, FontStyle.Bold)
            };

            _suggestionsBox = new ListBox
            {
                Dock = DockStyle.Fill
            };

            panel.Controls.Add(_suggestionsBox);
            panel.Controls.Add(suggestionsLabel);
            panel.Controls.Add(_modelStatusLabel);
            panel.Controls.Add(_sampleLabel);
            panel.Controls.Add(_knowledgeLabel);
            panel.Controls.Add(_testCoverageLabel);
            panel.Controls.Add(_columnAccuracyLabel);
            panel.Controls.Add(_generatedAtLabel);

            Controls.Add(panel);
            ResumeLayout(false);
        }

        private Label _generatedAtLabel;
        private Label _columnAccuracyLabel;
        private Label _testCoverageLabel;
        private Label _knowledgeLabel;
        private Label _sampleLabel;
        private Label _modelStatusLabel;
        private ListBox _suggestionsBox;
    }
}
