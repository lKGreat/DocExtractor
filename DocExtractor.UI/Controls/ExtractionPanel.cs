using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using DocExtractor.Core.Interfaces;
using DocExtractor.Core.Models;
using DocExtractor.Core.Models.Preview;
using DocExtractor.UI.Context;
using DocExtractor.UI.Forms;
using DocExtractor.UI.Helpers;
namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// Data extraction panel: file list, quick preview, batch extraction, results grid, recommendation.
    /// </summary>
    public partial class ExtractionPanel : UserControl
    {
        private readonly DocExtractorContext _ctx;
        private List<ExtractedRecord> _lastResults = new List<ExtractedRecord>();
        private List<ExtractedRecord> _displayedResults = new List<ExtractedRecord>();
        private int _lastManualTrainingSuggestionCount;

        internal ExtractionPanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            WireEvents();
            _ctx.LogLine += line => AppendToLog(_logBox, line);
        }

        public void OnActivated()
        {
            RefreshRecommendCombo();
        }

        // ── Event Wiring ──────────────────────────────────────────────────────

        private void WireEvents()
        {
            _addFilesBtn.Click += OnAddFiles;
            _removeFileBtn.Click += (s, e) => RemoveSelectedFiles();
            _clearFilesBtn.Click += (s, e) => _fileListBox.Items.Clear();
            _previewBtn.Click += OnQuickPreview;
            _runBtn.Click += OnRunExtraction;
            _exportBtn.Click += OnExport;
            _resultSearchBox.TextChanged += (s, e) => ApplyResultFilter();
            _recommendBtn.Click += OnRecommend;

            _fileListBox.AllowDrop = true;
            _fileListBox.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            _fileListBox.DragDrop += (s, e) => { var files = e.Data?.GetData(DataFormats.FileDrop) as string[]; if (files != null) AddFiles(files); };
            _fileListBox.MouseDown += OnFileListMouseDown;

            _fileContextMenu.Opening += OnContextMenuOpening;
            _removeFileMenuItem.Click += (s, e) => RemoveSelectedFiles();
            _clearAllMenuItem.Click += (s, e) => _fileListBox.Items.Clear();
        }

        // ── File List ─────────────────────────────────────────────────────────

        private void OnAddFiles(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Word/Excel 文件|*.docx;*.xlsx;*.xls|Word 文档|*.docx|Excel 表格|*.xlsx;*.xls|所有文件|*.*"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
                AddFiles(dlg.FileNames);
        }

        private void AddFiles(string[] files)
        {
            foreach (var f in files)
                if (!_fileListBox.Items.Contains(f))
                    _fileListBox.Items.Add(f);
        }

        private void RemoveSelectedFiles()
        {
            var toRemove = _fileListBox.SelectedItems.Cast<string>().ToList();
            toRemove.ForEach(f => _fileListBox.Items.Remove(f));
        }

        private void OnFileListMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            int idx = _fileListBox.IndexFromPoint(e.Location);
            if (idx >= 0 && !_fileListBox.GetSelected(idx))
            {
                for (int i = 0; i < _fileListBox.Items.Count; i++)
                    _fileListBox.SetSelected(i, false);
                _fileListBox.SetSelected(idx, true);
            }
        }

        private void OnContextMenuOpening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            int n = _fileListBox.SelectedItems.Count;
            _removeFileMenuItem.Text = n > 0 ? $"移除选中文件（{n} 个）" : "移除选中文件";
            _removeFileMenuItem.Enabled = n > 0;
            _clearAllMenuItem.Enabled = _fileListBox.Items.Count > 0;
        }

        // ── Extraction ────────────────────────────────────────────────────────

        private async void OnQuickPreview(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0) { MessageHelper.Warn(this, "请先添加要预览的文件"); return; }

            string filePath = _fileListBox.SelectedItem?.ToString() ?? _fileListBox.Items[0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath)) return;

            _previewBtn.Enabled = false;
            try
            {
                _ctx.Logger?.LogInformation($"开始快速预览：{Path.GetFileName(filePath)}");
                var preview = await Task.Run(() =>
                    _ctx.ExtractionService.Preview(filePath, _ctx.CurrentConfig, _ctx.ColumnModel, _ctx.SectionModel));

                if (!preview.Success) { MessageHelper.Error(this, $"预览失败：{preview.ErrorMessage}"); return; }

                LogPreviewResults(filePath, preview);
                HandleLowConfidenceMappings(filePath, preview);
                ShowPreviewNotification(preview);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"预览失败：{ex.Message}"); }
            finally { _previewBtn.Enabled = true; }
        }

        private void LogPreviewResults(string filePath, ExtractionPreviewResult preview)
        {
            _ctx.Logger?.LogInformation($"预览完成：{Path.GetFileName(filePath)} | 表格 {preview.Tables.Count} 个");
            foreach (var table in preview.Tables)
            {
                int mapped = table.Columns.Count(c => !string.IsNullOrWhiteSpace(c.MappedFieldName));
                int low = table.Columns.Count(c => c.IsLowConfidence);
                _ctx.Logger?.LogInformation($"  表格{table.TableIndex + 1} ({table.RowCount}x{table.ColCount}) 结构={table.SchemaType} 匹配列 {mapped}/{table.Columns.Count}，低置信度 {low}");
            }
            foreach (var w in preview.Warnings.Take(10))
                _ctx.Logger?.LogInformation($"  [预览警告] {w}");
        }

        private void ShowPreviewNotification(ExtractionPreviewResult preview)
        {
            if (preview.Warnings.Count > 0)
                MessageHelper.Warn(this, $"预览完成：发现 {preview.Warnings.Count} 个低置信度列，请检查配置或手工修正列映射。");
            else
                MessageHelper.Success(this, "预览完成：列映射状态良好。");
        }

        private void HandleLowConfidenceMappings(string filePath, ExtractionPreviewResult preview)
        {
            var lowItems = preview.Tables.SelectMany(t => t.Columns)
                .Where(c => c.IsLowConfidence && !string.IsNullOrWhiteSpace(c.RawColumnName)).ToList();
            if (lowItems.Count == 0) return;

            using var form = new ColumnMappingReviewForm(lowItems, _ctx.CurrentConfig.Fields);
            if (form.ShowDialog(this) != DialogResult.OK) return;

            int saved = _ctx.TrainingService.SaveManualColumnMappings(_ctx.DbPath, form.Corrections);
            if (saved <= 0) return;

            _ctx.Logger?.LogInformation($"已保存 {saved} 条手工列映射标注（来源：{Path.GetFileName(filePath)}）");
            MessageHelper.Success(this, $"已保存 {saved} 条映射标注");

            int verified = _ctx.TrainingService.GetVerifiedManualSampleCount(_ctx.DbPath);
            if (verified >= 25 && verified != _lastManualTrainingSuggestionCount && verified % 25 == 0)
            {
                _lastManualTrainingSuggestionCount = verified;
                MessageHelper.Info(this, $"已积累 {verified} 条人工确认标注，建议立即重训列名模型。");
            }
        }

        private async void OnRunExtraction(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0) { MessageHelper.Warn(this, "请先添加要处理的 Word/Excel 文件"); return; }

            SetExtractionUiState(false);
            _resultGrid.Rows.Clear();
            _resultGrid.Columns.Clear();
            _lastResults.Clear();
            _displayedResults.Clear();
            _resultSearchBox.Text = string.Empty;

            var files = _fileListBox.Items.Cast<string>().ToList();
            var config = _ctx.CurrentConfig;
            var progress = new Progress<PipelineProgress>(p =>
            {
                _progressBar.Value = Math.Min(p.Percent, 100);
                _ctx.NotifyStatus($"[{p.Stage}] {p.Message}");
                _ctx.Logger?.LogInformation($"[{p.Stage}] {p.Message}");
            });

            try
            {
                var results = await Task.Run(() =>
                    _ctx.ExtractionService.ExecuteBatch(files, config, _ctx.ColumnModel, _ctx.NerModel, _ctx.SectionModel, progress));

                ProcessExtractionResults(results, config);
            }
            catch (Exception ex)
            {
                _ctx.Logger?.LogError(ex, "抽取失败");
                MessageHelper.Error(this, $"抽取失败：{ex.Message}");
            }
            finally
            {
                SetExtractionUiState(true);
                _progressBar.Value = 0;
            }
        }

        private void ProcessExtractionResults(System.Collections.Generic.IReadOnlyList<DocExtractor.Core.Interfaces.ExtractionResult> results, ExtractionConfig config)
        {
            foreach (var r in results.Where(r => !r.Success))
                _ctx.Logger?.LogError($"[错误] {Path.GetFileName(r.SourceFile)}: {r.ErrorMessage}");
            foreach (var r in results.Where(r => r.Warnings.Count > 0))
                foreach (var w in r.Warnings)
                    _ctx.Logger?.LogWarning($"[警告] {Path.GetFileName(r.SourceFile)}: {w}");

            _lastResults = results.SelectMany(r => r.Records).ToList();
            _displayedResults = _lastResults.Where(r => r.IsComplete).ToList();
            ShowResults(_displayedResults, config.Fields);

            int total = _lastResults.Count, complete = _displayedResults.Count;
            _ctx.Logger?.LogInformation($"\n完成！共抽取 {total} 条记录（完整: {complete}，不完整: {total - complete}，列表仅显示完整记录）");
            _ctx.NotifyStatus($"完成 | 完整 {complete}/{total} 条记录");
            _exportBtn.Enabled = complete > 0;

            AutoLearnGroupKnowledge(_displayedResults);

            if (complete > 0)
                MessageHelper.Success(this, $"抽取完成，共 {total} 条（完整 {complete} 条已显示）");
            else if (results.Any(r => !r.Success))
                MessageHelper.Error(this, $"抽取失败：{results.First(r => !r.Success).ErrorMessage}");
            else
                MessageHelper.Warn(this, "未抽取到数据，请检查配置的字段列名变体是否匹配文档表格列头");
        }

        private void SetExtractionUiState(bool idle)
        {
            _runBtn.Enabled = idle;
            _exportBtn.Enabled = idle && _displayedResults.Count > 0;
        }

        private void OnExport(object sender, EventArgs e)
        {
            var toExport = _lastResults.Where(r => r.IsComplete).ToList();
            if (toExport.Count == 0) return;

            using var selectionForm = new ExportFieldSelectionForm(_ctx.CurrentConfig.Fields);
            if (selectionForm.ShowDialog(this) != DialogResult.OK) return;
            if (selectionForm.SelectedFieldNames.Count == 0) { MessageHelper.Warn(this, "请至少选择一个导出字段"); return; }

            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = $"抽取结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var exporter = new DocExtractor.Data.Export.ExcelExporter();
                exporter.Export(toExport, _ctx.CurrentConfig.Fields, dlg.FileName, selectionForm.SelectedFieldNames);
                _ctx.Logger?.LogInformation($"已导出到: {dlg.FileName}");
                MessageHelper.Success(this, "导出成功！");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导出失败：{ex.Message}"); }
        }

        // ── Results Display ───────────────────────────────────────────────────

        private void ShowResults(List<ExtractedRecord> records, IReadOnlyList<FieldDefinition> fields)
        {
            _resultGrid.Columns.Clear();
            _resultGrid.Rows.Clear();
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Source", HeaderText = "来源文件" });
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Complete", HeaderText = "完整" });

            AddFieldColumns(fields);
            AddGroupNameFallback(records, fields, out int groupNameColIdx);

            foreach (var r in records)
                AddResultRow(r, fields, groupNameColIdx);
        }

        private void AddFieldColumns(IReadOnlyList<FieldDefinition> fields)
        {
            foreach (var f in fields)
            {
                var col = new DataGridViewTextBoxColumn { Name = f.FieldName, HeaderText = string.IsNullOrEmpty(f.DisplayName) ? f.FieldName : f.DisplayName };
                if (f.FieldName == "GroupName") StyleGroupNameColumn(col);
                _resultGrid.Columns.Add(col);
            }
        }

        private void StyleGroupNameColumn(DataGridViewTextBoxColumn col)
        {
            col.HeaderCell.Style.BackColor = Color.FromArgb(0, 176, 240);
            col.HeaderCell.Style.ForeColor = Color.White;
            col.DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 255);
            col.DefaultCellStyle.Font = new Font(_resultGrid.Font, FontStyle.Bold);
            col.MinimumWidth = 140;
        }

        private void AddGroupNameFallback(List<ExtractedRecord> records, IReadOnlyList<FieldDefinition> fields, out int groupNameColIdx)
        {
            groupNameColIdx = -1;
            bool hasCol = fields.Any(f => f.FieldName == "GroupName");
            bool hasData = records.Any(r => r.Fields.ContainsKey("GroupName") && !string.IsNullOrWhiteSpace(r.Fields["GroupName"]));
            if (hasCol || !hasData) return;

            var col = new DataGridViewTextBoxColumn { Name = "GroupName", HeaderText = "组名" };
            StyleGroupNameColumn(col);
            _resultGrid.Columns.Insert(2, col);
            groupNameColIdx = 2;
        }

        private void AddResultRow(ExtractedRecord r, IReadOnlyList<FieldDefinition> fields, int groupNameColIdx)
        {
            var row = new DataGridViewRow();
            row.CreateCells(_resultGrid);
            row.Cells[0].Value = Path.GetFileName(r.SourceFile);
            row.Cells[1].Value = r.IsComplete ? "\u2713" : "\u2717";
            if (!r.IsComplete) row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);

            int offset = 0;
            if (groupNameColIdx >= 0) { row.Cells[groupNameColIdx].Value = r.GetField("GroupName"); offset = 1; }

            for (int i = 0; i < fields.Count; i++)
                row.Cells[i + 2 + offset].Value = r.GetField(fields[i].FieldName);

            _resultGrid.Rows.Add(row);
        }

        private void ApplyResultFilter()
        {
            if (_displayedResults.Count == 0) return;
            string kw = _resultSearchBox.Text?.Trim() ?? string.Empty;
            if (kw.Length == 0) { ShowResults(_displayedResults, _ctx.CurrentConfig.Fields); return; }

            var filtered = _displayedResults.Where(r =>
                Path.GetFileName(r.SourceFile).IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                || r.Fields.Any(kv =>
                    kv.Key.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0
                    || (!string.IsNullOrWhiteSpace(kv.Value) && kv.Value.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();

            ShowResults(filtered, _ctx.CurrentConfig.Fields);
        }

        // ── Recommendation ────────────────────────────────────────────────────

        private void AutoLearnGroupKnowledge(List<ExtractedRecord> completeResults)
        {
            try
            {
                var withGroup = completeResults.Where(r =>
                    r.Fields.ContainsKey("GroupName") && !string.IsNullOrWhiteSpace(r.Fields["GroupName"])).ToList();
                if (withGroup.Count == 0) return;

                var summary = _ctx.RecommendationService.AutoLearnGroupKnowledge(_ctx.DbPath, withGroup);
                foreach (var detail in summary.FileDetails)
                    _ctx.Logger?.LogInformation($"知识库{(detail.WasReplaced ? "更新" : "新录")}：{Path.GetFileName(detail.SourceFile)} → {detail.GroupCount} 个组 / {detail.InsertedCount} 条细则");

                if (summary.ReplacedFiles > 0)
                    _ctx.Logger?.LogInformation($"[去重] {summary.ReplacedFiles} 个文件已存在旧记录，已自动清空后重新录入");

                _ctx.Logger?.LogInformation($"知识库合计学习 {summary.TotalGroups} 个组、{summary.TotalInserted} 条记录");
                RefreshRecommendCombo();

                _resultTabs.SelectedIndex = 1;
                if (_recommendGroupCombo.Items.Count > 0 && _recommendGroupCombo.SelectedIndex < 0)
                    _recommendGroupCombo.SelectedIndex = 0;
            }
            catch (Exception ex) { _ctx.Logger?.LogWarning($"知识库学习失败: {ex.Message}"); }
        }

        private void RefreshRecommendCombo()
        {
            try
            {
                var items = _ctx.RecommendationService.BuildRecommendGroups(_ctx.DbPath, _lastResults);
                _recommendGroupCombo.Items.Clear();
                foreach (var item in items) _recommendGroupCombo.Items.Add(item);
            }
            catch { }
        }

        private void OnRecommend(object sender, EventArgs e)
        {
            string groupName = _recommendGroupCombo.Text?.Trim();
            if (string.IsNullOrWhiteSpace(groupName)) { MessageHelper.Warn(this, "请输入或选择一个组名"); return; }

            _recommendGrid.Rows.Clear();
            try
            {
                var response = _ctx.RecommendationService.Recommend(_ctx.DbPath, groupName);
                _recommendCountLabel.Text = $"知识库：{response.KnowledgeCount} 条";

                bool hasItems = response.Items.Count > 0;
                _recommendHintLabel.Visible = !hasItems;
                _recommendGrid.Visible = hasItems;
                if (!hasItems) return;

                for (int i = 0; i < response.Items.Count; i++)
                    AddRecommendRow(i, response.Items[i]);

                _ctx.Logger?.LogInformation($"推荐完成：组名「{groupName}」→ {response.Items.Count} 条推荐项");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"推荐失败：{ex.Message}"); }
        }

        private void AddRecommendRow(int index, DocExtractor.ML.Recommendation.RecommendedItem item)
        {
            int rowIdx = _recommendGrid.Rows.Add(
                (index + 1).ToString(),
                item.ItemName,
                item.TypicalRequiredValue ?? "",
                $"{item.Confidence:P1}",
                $"{item.OccurrenceCount} 次",
                string.Join(", ", item.SourceFiles.Select(f => Path.GetFileName(f))));

            var row = _recommendGrid.Rows[rowIdx];
            if (item.Confidence >= 0.8f) row.DefaultCellStyle.BackColor = Color.FromArgb(230, 255, 230);
            else if (item.Confidence >= 0.5f) row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 220);
            else row.DefaultCellStyle.ForeColor = Color.Gray;
        }

        // ── Logging Helpers ───────────────────────────────────────────────────

        private static void AppendToLog(RichTextBox box, string line)
        {
            if (box.InvokeRequired) { box.Invoke(new Action<RichTextBox, string>(AppendToLog), box, line); return; }
            box.AppendText(line + Environment.NewLine);
            box.ScrollToCaret();
        }
    }
}
