using System;
using DocExtractor.Data.Repositories;

namespace DocExtractor.UI.Forms
{
    public partial class DiagnosticsForm : System.Windows.Forms.Form
    {
        private readonly string _dbPath;
        private readonly string _modelsDir;

        public DiagnosticsForm(string dbPath, string modelsDir)
        {
            _dbPath = dbPath;
            _modelsDir = modelsDir;
            InitializeComponent();
            LoadSnapshot();
        }

        private void LoadSnapshot()
        {
            try
            {
                var repo = new KpiRepository(_dbPath, _modelsDir);
                var snapshot = repo.GetSnapshot();

                _generatedAtLabel.Text = $"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
                _columnAccuracyLabel.Text = $"列名识别准确率：{(snapshot.ColumnAccuracy.HasValue ? snapshot.ColumnAccuracy.Value.ToString("F1") + "%" : "N/A")}";
                _knowledgeLabel.Text = $"知识库规模：{snapshot.KnowledgeCount}";
                _testCoverageLabel.Text = $"测试覆盖率：{snapshot.TestCoverage:F1}%";
                _sampleLabel.Text = $"样本：列名 {snapshot.ColumnSamples} / NER {snapshot.NerSamples} / 章节 {snapshot.SectionSamples}";
                _modelStatusLabel.Text = $"模型状态：列名[{BoolText(snapshot.ColumnModelExists)}] NER[{BoolText(snapshot.NerModelExists)}] 章节[{BoolText(snapshot.SectionModelExists)}]";

                _suggestionsBox.Items.Clear();
                foreach (var suggestion in snapshot.Suggestions)
                    _suggestionsBox.Items.Add(suggestion);
            }
            catch (Exception ex)
            {
                _generatedAtLabel.Text = "诊断加载失败";
                _suggestionsBox.Items.Clear();
                _suggestionsBox.Items.Add(ex.Message);
            }
        }

        private static string BoolText(bool value) => value ? "已训练" : "未训练";
    }
}
