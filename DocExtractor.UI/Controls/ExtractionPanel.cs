using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
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
    public partial class ExtractionPanel : UserControl
    {
        private readonly DocExtractorContext _ctx;

        // ── State ─────────────────────────────────────────────────────────────
        private List<ExtractedRecord> _lastResults = new List<ExtractedRecord>();
        private List<ExtractedRecord> _displayedResults = new List<ExtractedRecord>();
        private int _lastManualTrainingSuggestionCount;
        private int _totalComplete;
        private int _totalRecords;

        // ── Cancellation / Debounce ───────────────────────────────────────────
        private CancellationTokenSource? _extractionCts;
        private System.Windows.Forms.Timer _previewDebounceTimer;

        // ── Preview state ─────────────────────────────────────────────────────
        private ExtractionPreviewResult? _currentPreviewResult;

        internal ExtractionPanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            InitDebounceTimer();
            WireEvents();
            _ctx.LogLine += line => AppendToLog(_logBox, line);
            _ctx.ConfigChanged += OnConfigChangedForPreview;
        }

        public void OnActivated() => RefreshRecommendCombo();

        // ── Init ──────────────────────────────────────────────────────────────

        private void InitDebounceTimer()
        {
            _previewDebounceTimer = new System.Windows.Forms.Timer { Interval = 350 };
            _previewDebounceTimer.Tick += async (s, e) =>
            {
                _previewDebounceTimer.Stop();
                await RunAutoPreviewAsync();
            };
        }

        private void WireEvents()
        {
            _addFilesBtn.Click += OnAddFiles;
            _removeFileBtn.Click += (s, e) => RemoveSelectedFiles();
            _clearFilesBtn.Click += (s, e) => _fileListBox.Items.Clear();
            _previewBtn.Click += OnQuickPreview;
            _runBtn.Click += OnRunExtraction;
            _stopBtn.Click += OnStopExtraction;
            _exportBtn.Click += OnExport;
            _resultSearchBox.TextChanged += (s, e) => ApplyResultFilter();
            _recommendBtn.Click += OnRecommend;

            _fileListBox.SelectedIndexChanged += (s, e) => ScheduleAutoPreview();
            _tableListBox.SelectedIndexChanged += OnTableSelected;

            _fileListBox.AllowDrop = true;
            _fileListBox.DragEnter += (s, e) => { if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true) e.Effect = DragDropEffects.Copy; };
            _fileListBox.DragDrop += (s, e) => { var files = e.Data?.GetData(DataFormats.FileDrop) as string[]; if (files != null) AddFiles(files); };
            _fileListBox.MouseDown += OnFileListMouseDown;

            _fileContextMenu.Opening += OnContextMenuOpening;
            _removeFileMenuItem.Click += (s, e) => RemoveSelectedFiles();
            _clearAllMenuItem.Click += (s, e) => _fileListBox.Items.Clear();

            _resultTabs.SelectedIndexChanged += OnTabSelected;

            _moveUpBtn.Click += OnMoveFieldUp;
            _moveDownBtn.Click += OnMoveFieldDown;
            _splitModeCombo.SelectedIndexChanged += OnSplitModeChanged;
            _saveOutputConfigBtn.Click += OnSaveOutputConfig;
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
                for (int i = 0; i < _fileListBox.Items.Count; i++) _fileListBox.SetSelected(i, false);
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

        // ── Auto Preview ──────────────────────────────────────────────────────

        private void ScheduleAutoPreview()
        {
            _previewDebounceTimer.Stop();
            _previewDebounceTimer.Start();
        }

        private void OnConfigChangedForPreview()
        {
            if (_fileListBox.SelectedItem != null)
                ScheduleAutoPreview();
        }

        private async Task RunAutoPreviewAsync()
        {
            string? filePath = _fileListBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

            try
            {
                var preview = await Task.Run(() =>
                    _ctx.ExtractionService.Preview(filePath, _ctx.CurrentConfig, _ctx.ColumnModel, _ctx.SectionModel));
                _currentPreviewResult = preview;
                FillTableList(preview);
                if (preview.Tables.Count > 0)
                    ShowTablePreview(0);
                _resultTabs.SelectedTab = _previewTab;
            }
            catch (Exception ex)
            {
                _ctx.Logger?.LogWarning($"自动预览失败: {ex.Message}");
            }
        }

        private void FillTableList(ExtractionPreviewResult preview)
        {
            _tableListBox.Items.Clear();
            foreach (var t in preview.Tables)
            {
                string title = string.IsNullOrWhiteSpace(t.Title) ? $"表格 {t.TableIndex + 1}" : t.Title;
                int low = t.Columns.Count(c => c.IsLowConfidence);
                string warn = low > 0 ? $" ⚠{low}" : "";
                _tableListBox.Items.Add($"[T{t.TableIndex + 1}] {title} ({t.RowCount}行×{t.ColCount}列){warn}");
            }
            if (_tableListBox.Items.Count > 0) _tableListBox.SelectedIndex = 0;
        }

        private void OnTableSelected(object sender, EventArgs e)
        {
            int idx = _tableListBox.SelectedIndex;
            if (_currentPreviewResult == null || idx < 0 || idx >= _currentPreviewResult.Tables.Count) return;
            ShowTablePreview(idx);
        }

        private void ShowTablePreview(int tableIdx)
        {
            if (_currentPreviewResult == null) return;
            var table = _currentPreviewResult.Tables[tableIdx];
            FillColumnMapGrid(table);
            FillDataPreviewGrid(table);
        }

        private void FillColumnMapGrid(TablePreviewInfo table)
        {
            _columnMapGrid.Rows.Clear();
            var fieldOptions = _ctx.CurrentConfig.Fields
                .Select(f => string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldName : $"{f.DisplayName} ({f.FieldName})")
                .ToArray();

            foreach (var col in table.Columns)
            {
                int rowIdx = _columnMapGrid.Rows.Add(
                    col.RawColumnName,
                    col.MappedDisplayName ?? col.MappedFieldName ?? "—",
                    col.MatchMethod,
                    col.Confidence > 0 ? $"{col.Confidence:P0}" : "—",
                    string.Empty);

                var fixCell = _columnMapGrid.Rows[rowIdx].Cells["ColFix"] as DataGridViewComboBoxCell;
                if (fixCell != null)
                {
                    fixCell.Items.Clear();
                    fixCell.Items.AddRange(fieldOptions);
                }

                if (col.IsLowConfidence)
                    _columnMapGrid.Rows[rowIdx].DefaultCellStyle.BackColor = Color.FromArgb(255, 245, 220);
                else if (!string.IsNullOrWhiteSpace(col.MappedFieldName))
                    _columnMapGrid.Rows[rowIdx].DefaultCellStyle.ForeColor = Color.FromArgb(30, 120, 50);
            }
        }

        private void FillDataPreviewGrid(TablePreviewInfo table)
        {
            _dataPreviewGrid.Columns.Clear();
            _dataPreviewGrid.Rows.Clear();
            if (_currentPreviewResult == null) return;

            string? filePath = _fileListBox.SelectedItem?.ToString();
            if (string.IsNullOrWhiteSpace(filePath)) return;

            try
            {
                var parser = _ctx.ExtractionService.GetParserForFile(filePath, _ctx.CurrentConfig);
                if (parser == null) return;
                var rawTables = parser.Parse(filePath);
                if (table.TableIndex >= rawTables.Count) return;

                var raw = rawTables[table.TableIndex];
                int previewRows = Math.Min(6, raw.RowCount);
                for (int c = 0; c < raw.ColCount; c++)
                {
                    string header = raw.GetValue(0, c);
                    _dataPreviewGrid.Columns.Add($"C{c}", string.IsNullOrWhiteSpace(header) ? $"列{c + 1}" : header);
                }
                for (int r = 1; r < previewRows; r++)
                {
                    var row = _dataPreviewGrid.Rows.Add();
                    for (int c = 0; c < raw.ColCount; c++)
                        _dataPreviewGrid.Rows[row].Cells[c].Value = raw.GetValue(r, c);
                }
            }
            catch { /* 预览失败不影响主流程 */ }
        }

        // ── Quick Preview Button ──────────────────────────────────────────────

        private async void OnQuickPreview(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0) { MessageHelper.Warn(this, "请先添加要预览的文件"); return; }
            string filePath = _fileListBox.SelectedItem?.ToString() ?? _fileListBox.Items[0]?.ToString() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(filePath)) return;

            _previewBtn.Enabled = false;
            try
            {
                var preview = await Task.Run(() =>
                    _ctx.ExtractionService.Preview(filePath, _ctx.CurrentConfig, _ctx.ColumnModel, _ctx.SectionModel));
                _currentPreviewResult = preview;
                FillTableList(preview);
                if (preview.Tables.Count > 0) ShowTablePreview(0);
                _resultTabs.SelectedTab = _previewTab;
                HandleLowConfidenceMappings(filePath, preview);
                ShowPreviewNotification(preview);
            }
            catch (Exception ex) { MessageHelper.Error(this, $"预览失败：{ex.Message}"); }
            finally { _previewBtn.Enabled = true; }
        }

        private void ShowPreviewNotification(ExtractionPreviewResult preview)
        {
            if (preview.Warnings.Count > 0)
                MessageHelper.Warn(this, $"预览完成：发现 {preview.Warnings.Count} 个低置信度列，请检查配置。");
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

        // ── Extraction ────────────────────────────────────────────────────────

        private async void OnRunExtraction(object sender, EventArgs e)
        {
            if (_fileListBox.Items.Count == 0) { MessageHelper.Warn(this, "请先添加要处理的 Word/Excel 文件"); return; }

            _extractionCts = new CancellationTokenSource();
            SetExtractionUiState(false);
            _resultGrid.Rows.Clear();
            _resultGrid.Columns.Clear();
            _lastResults.Clear();
            _displayedResults.Clear();
            _totalComplete = 0;
            _totalRecords = 0;
            _resultSearchBox.Text = string.Empty;
            PrepareResultGridColumns(_ctx.CurrentConfig.Fields);

            var files = _fileListBox.Items.Cast<string>().ToList();
            var config = _ctx.CurrentConfig;
            var token = _extractionCts.Token;
            var progress = new Progress<PipelineProgress>(p => HandlePipelineProgress(p, config));

            try
            {
                var results = await Task.Run(() =>
                    _ctx.ExtractionService.ExecuteBatch(files, config,
                        _ctx.ColumnModel, _ctx.NerModel, _ctx.SectionModel,
                        progress, token), token);

                FinalizeExtractionResults(results, config);
            }
            catch (OperationCanceledException)
            {
                MessageHelper.Warn(this, $"抽取已取消（已处理 {_totalRecords} 条记录）");
                _ctx.NotifyStatus($"已取消 | 已获取 {_totalComplete}/{_totalRecords} 条");
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
                _extractionCts?.Dispose();
                _extractionCts = null;
            }
        }

        private void OnStopExtraction(object sender, EventArgs e)
        {
            _extractionCts?.Cancel();
            _stopBtn.Enabled = false;
        }

        private void HandlePipelineProgress(PipelineProgress p, ExtractionConfig config)
        {
            _progressBar.Value = Math.Min(p.Percent, 100);
            _ctx.NotifyStatus($"[{p.Stage}] {p.Message}");
            _ctx.Logger?.LogInformation($"[{p.Stage}] {p.Message}");

            if (p.IncrementalResult != null)
                AppendIncrementalResults(p.IncrementalResult, config);
        }

        private void AppendIncrementalResults(ExtractionResult result, ExtractionConfig config)
        {
            _lastResults.AddRange(result.Records);
            var complete = result.Records.Where(r => r.IsComplete).ToList();
            _displayedResults.AddRange(complete);
            _totalRecords += result.Records.Count;
            _totalComplete += complete.Count;

            foreach (var r in complete)
                AppendResultRow(r, config.Fields);

            UpdateLiveStats();
            _resultTabs.SelectedTab = _resultTab;
        }

        private void UpdateLiveStats()
        {
            _statsLabel.Text = $"完整 {_totalComplete} / 总计 {_totalRecords} 条";
        }

        private void FinalizeExtractionResults(
            IReadOnlyList<ExtractionResult> results,
            ExtractionConfig config)
        {
            foreach (var r in results.Where(r => !r.Success))
                _ctx.Logger?.LogError($"[错误] {Path.GetFileName(r.SourceFile)}: {r.ErrorMessage}");
            foreach (var r in results.Where(r => r.Warnings.Count > 0))
                foreach (var w in r.Warnings)
                    _ctx.Logger?.LogWarning($"[警告] {Path.GetFileName(r.SourceFile)}: {w}");

            _ctx.Logger?.LogInformation($"\n完成！共 {_totalRecords} 条（完整: {_totalComplete}，不完整: {_totalRecords - _totalComplete}）");
            _ctx.NotifyStatus($"完成 | 完整 {_totalComplete}/{_totalRecords} 条记录");
            _exportBtn.Enabled = _totalComplete > 0;

            AutoLearnGroupKnowledge(_displayedResults);

            if (_totalComplete > 0)
                MessageHelper.Success(this, $"抽取完成，共 {_totalRecords} 条（完整 {_totalComplete} 条已显示）");
            else if (results.Any(r => !r.Success))
                MessageHelper.Error(this, $"抽取失败：{results.First(r => !r.Success).ErrorMessage}");
            else
                MessageHelper.Warn(this, "未抽取到数据，请检查配置的字段列名变体是否匹配文档表格列头");
        }

        private void SetExtractionUiState(bool idle)
        {
            _runBtn.Enabled = idle;
            _stopBtn.Enabled = !idle;
            _exportBtn.Enabled = idle && _displayedResults.Count > 0;
        }

        // ── Results Display ───────────────────────────────────────────────────

        private void PrepareResultGridColumns(IReadOnlyList<FieldDefinition> fields)
        {
            _resultGrid.Columns.Clear();
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Source", HeaderText = "来源文件", FillWeight = 15 });
            _resultGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "_Complete", HeaderText = "完整", FillWeight = 5 });
            foreach (var f in fields)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    Name = f.FieldName,
                    HeaderText = string.IsNullOrEmpty(f.DisplayName) ? f.FieldName : f.DisplayName
                };
                if (f.FieldName == "GroupName") StyleGroupNameColumn(col);
                _resultGrid.Columns.Add(col);
            }
        }

        private void ShowResults(List<ExtractedRecord> records, IReadOnlyList<FieldDefinition> fields)
        {
            _resultGrid.Rows.Clear();
            _resultGrid.Columns.Clear();
            PrepareResultGridColumns(fields);
            foreach (var r in records)
                AppendResultRow(r, fields);
        }

        private void AppendResultRow(ExtractedRecord r, IReadOnlyList<FieldDefinition> fields)
        {
            var row = new DataGridViewRow();
            row.CreateCells(_resultGrid);
            row.Cells[0].Value = Path.GetFileName(r.SourceFile);
            row.Cells[1].Value = r.IsComplete ? "\u2713" : "\u2717";
            if (!r.IsComplete) row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);

            for (int i = 0; i < fields.Count; i++)
            {
                int colIdx = i + 2;
                if (colIdx < row.Cells.Count)
                    row.Cells[colIdx].Value = r.GetField(fields[i].FieldName);
            }
            _resultGrid.Rows.Add(row);
        }

        private void StyleGroupNameColumn(DataGridViewTextBoxColumn col)
        {
            col.HeaderCell.Style.BackColor = Color.FromArgb(0, 176, 240);
            col.HeaderCell.Style.ForeColor = Color.White;
            col.DefaultCellStyle.BackColor = Color.FromArgb(235, 250, 255);
            col.DefaultCellStyle.Font = new Font(_resultGrid.Font, FontStyle.Bold);
            col.MinimumWidth = 140;
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
                    || (!string.IsNullOrWhiteSpace(kv.Value) &&
                        kv.Value.IndexOf(kw, StringComparison.OrdinalIgnoreCase) >= 0))).ToList();

            ShowResults(filtered, _ctx.CurrentConfig.Fields);
        }

        // ── Output Config Tab ─────────────────────────────────────────────────

        private void OnTabSelected(object sender, EventArgs e)
        {
            if (_resultTabs.SelectedTab == _outputConfigTab)
                FillOutputConfigTab();
        }

        private void FillOutputConfigTab()
        {
            var config = _ctx.CurrentConfig;
            var outputCfg = config.OutputConfig;

            // Build output mappings from current config fields (merge with saved mappings)
            _outputFieldGrid.Rows.Clear();
            for (int i = 0; i < config.Fields.Count; i++)
            {
                var f = config.Fields[i];
                var mapping = outputCfg?.FieldMappings.FirstOrDefault(m => m.SourceFieldName == f.FieldName);
                bool enabled = mapping?.IsEnabled ?? true;
                string outputName = mapping?.OutputColumnName
                    ?? (string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldName : f.DisplayName);
                _outputFieldGrid.Rows.Add(enabled, f.DisplayName.Length > 0 ? $"{f.DisplayName} ({f.FieldName})" : f.FieldName, outputName);
                _outputFieldGrid.Rows[i].Tag = f.FieldName;
            }

            // Fill sheet rule
            var sheetRule = outputCfg?.SheetRule ?? new OutputSheetRule();
            _splitModeCombo.SelectedIndex = ToSplitModeIndex(sheetRule.SplitMode);
            _sheetTemplateBox.Text = sheetRule.SheetNameTemplate;
            _summarySheetCheck.Checked = outputCfg?.IncludeSummarySheet ?? true;

            // Fill split field combo
            _splitFieldCombo.Items.Clear();
            foreach (var f in config.Fields)
                _splitFieldCombo.Items.Add(f.DisplayName.Length > 0 ? $"{f.DisplayName} ({f.FieldName})" : f.FieldName);

            if (!string.IsNullOrWhiteSpace(sheetRule.SplitFieldName))
            {
                int idx = config.Fields.FindIndex(f => f.FieldName == sheetRule.SplitFieldName);
                if (idx >= 0) _splitFieldCombo.SelectedIndex = idx;
            }

            UpdateSplitFieldVisibility();
        }

        private void OnSplitModeChanged(object sender, EventArgs e) => UpdateSplitFieldVisibility();

        private void UpdateSplitFieldVisibility()
        {
            bool byField = ToSplitMode(_splitModeCombo.SelectedIndex) == SheetSplitMode.ByField;
            _splitFieldLabel.Visible = byField;
            _splitFieldCombo.Visible = byField;
        }

        private void OnMoveFieldUp(object sender, EventArgs e) => MoveSelectedRow(-1);
        private void OnMoveFieldDown(object sender, EventArgs e) => MoveSelectedRow(1);

        private void MoveSelectedRow(int direction)
        {
            int idx = _outputFieldGrid.CurrentCell?.RowIndex ?? -1;
            int newIdx = idx + direction;
            if (idx < 0 || newIdx < 0 || newIdx >= _outputFieldGrid.Rows.Count) return;

            var r1 = _outputFieldGrid.Rows[idx];
            var r2 = _outputFieldGrid.Rows[newIdx];
            SwapRowValues(r1, r2);
            _outputFieldGrid.ClearSelection();
            _outputFieldGrid.Rows[newIdx].Selected = true;
        }

        private void SwapRowValues(DataGridViewRow r1, DataGridViewRow r2)
        {
            for (int c = 0; c < _outputFieldGrid.Columns.Count; c++)
            {
                var tmp = r1.Cells[c].Value;
                r1.Cells[c].Value = r2.Cells[c].Value;
                r2.Cells[c].Value = tmp;
            }
            var tmpTag = r1.Tag;
            r1.Tag = r2.Tag;
            r2.Tag = tmpTag;
        }

        private void OnSaveOutputConfig(object sender, EventArgs e)
        {
            var mappings = new List<OutputFieldMapping>();
            for (int i = 0; i < _outputFieldGrid.Rows.Count; i++)
            {
                var row = _outputFieldGrid.Rows[i];
                string fieldName = row.Tag?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(fieldName)) continue;
                mappings.Add(new OutputFieldMapping
                {
                    SourceFieldName = fieldName,
                    OutputColumnName = row.Cells["OFOutput"].Value?.ToString() ?? fieldName,
                    OutputOrder = i,
                    IsEnabled = row.Cells["OFEnable"].Value is true
                });
            }

            string splitFieldName = string.Empty;
            if (_splitModeCombo.SelectedIndex == 1 && _splitFieldCombo.SelectedIndex >= 0)
                splitFieldName = _ctx.CurrentConfig.Fields[_splitFieldCombo.SelectedIndex].FieldName;

            _ctx.CurrentConfig.OutputConfig = new OutputConfig
            {
                FieldMappings = mappings,
                IncludeSummarySheet = _summarySheetCheck.Checked,
                SheetRule = new OutputSheetRule
                {
                    SplitMode = ToSplitMode(_splitModeCombo.SelectedIndex),
                    SplitFieldName = splitFieldName,
                    SheetNameTemplate = _sheetTemplateBox.Text
                }
            };

            _ctx.ConfigService.Save(_ctx.CurrentConfig);
            MessageHelper.Success(this, "输出方案已保存");
        }

        private static int ToSplitModeIndex(SheetSplitMode mode)
        {
            // UI 顺序：按来源文件(0) / 按字段值(1) / 不切分(2)
            if (mode == SheetSplitMode.BySourceFile) return 0;
            if (mode == SheetSplitMode.ByField) return 1;
            return 2;
        }

        private static SheetSplitMode ToSplitMode(int selectedIndex)
        {
            if (selectedIndex == 0) return SheetSplitMode.BySourceFile;
            if (selectedIndex == 1) return SheetSplitMode.ByField;
            return SheetSplitMode.None;
        }

        // ── Export ────────────────────────────────────────────────────────────

        private void OnExport(object sender, EventArgs e)
        {
            var toExport = _lastResults.Where(r => r.IsComplete).ToList();
            if (toExport.Count == 0) return;

            // If no OutputConfig, show field selection dialog as before
            if (_ctx.CurrentConfig.OutputConfig == null || _ctx.CurrentConfig.OutputConfig.FieldMappings.Count == 0)
            {
                using var selectionForm = new ExportFieldSelectionForm(_ctx.CurrentConfig.Fields);
                if (selectionForm.ShowDialog(this) != DialogResult.OK) return;
                if (selectionForm.SelectedFieldNames.Count == 0) { MessageHelper.Warn(this, "请至少选择一个导出字段"); return; }
                ExportWithSelectedFields(toExport, selectionForm.SelectedFieldNames);
            }
            else
            {
                ExportWithOutputConfig(toExport, _ctx.CurrentConfig.OutputConfig);
            }
        }

        private void ExportWithSelectedFields(List<ExtractedRecord> records, List<string> fieldNames)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = $"抽取结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var exporter = new DocExtractor.Data.Export.ExcelExporter();
                exporter.Export(records, _ctx.CurrentConfig.Fields, dlg.FileName, fieldNames);
                _ctx.Logger?.LogInformation($"已导出到: {dlg.FileName}");
                MessageHelper.Success(this, "导出成功！");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导出失败：{ex.Message}"); }
        }

        private void ExportWithOutputConfig(List<ExtractedRecord> records, OutputConfig outputConfig)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = $"抽取结果_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            try
            {
                var exporter = new DocExtractor.Data.Export.ExcelExporter();
                exporter.Export(records, _ctx.CurrentConfig.Fields, dlg.FileName, outputConfig);
                _ctx.Logger?.LogInformation($"已导出到: {dlg.FileName}");
                MessageHelper.Success(this, "导出成功！");
            }
            catch (Exception ex) { MessageHelper.Error(this, $"导出失败：{ex.Message}"); }
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
                    _ctx.Logger?.LogInformation($"知识库{(detail.WasReplaced ? "更新" : "新录")}：{Path.GetFileName(detail.SourceFile)} → {detail.GroupCount} 个组");

                _ctx.Logger?.LogInformation($"知识库学习完成：{summary.TotalGroups} 组 / {summary.TotalInserted} 条");
                RefreshRecommendCombo();
                _resultTabs.SelectedIndex = _resultTabs.TabPages.IndexOf(_recommendTab);
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
            string groupName = (_recommendGroupCombo.Text ?? string.Empty).Trim();
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
                {
                    var item = response.Items[i];
                    int rowIdx = _recommendGrid.Rows.Add(
                        (i + 1).ToString(), item.ItemName, item.TypicalRequiredValue ?? "",
                        $"{item.Confidence:P1}", $"{item.OccurrenceCount} 次",
                        string.Join(", ", item.SourceFiles.Select(f => Path.GetFileName(f))));
                    var row = _recommendGrid.Rows[rowIdx];
                    if (item.Confidence >= 0.8f) row.DefaultCellStyle.BackColor = Color.FromArgb(230, 255, 230);
                    else if (item.Confidence < 0.5f) row.DefaultCellStyle.ForeColor = Color.Gray;
                }
            }
            catch (Exception ex) { MessageHelper.Error(this, $"推荐失败：{ex.Message}"); }
        }

        // ── Logging ───────────────────────────────────────────────────────────

        private static void AppendToLog(RichTextBox box, string line)
        {
            if (box.InvokeRequired) { box.Invoke(new Action<RichTextBox, string>(AppendToLog), box, line); return; }
            box.AppendText(line + Environment.NewLine);
            box.ScrollToCaret();
        }
    }
}
