using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocExtractor.Core.Protocol;
using DocExtractor.Data.Export;
using DocExtractor.UI.Context;
using DocExtractor.UI.Helpers;
using DocExtractor.UI.Services;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// Protocol document parser panel: analyzes satellite communication protocol
    /// Word documents and generates telemetry configuration Excel files.
    /// </summary>
    public partial class ProtocolParserPanel : UserControl
    {
        private enum ProtocolTemplateType
        {
            Telemetry,
            Telecommand
        }

        private readonly DocExtractorContext _ctx;
        private readonly ProtocolWorkflowService _service = new ProtocolWorkflowService();
        private string _selectedFilePath = "";
        private string _lastOutputDir = "";
        private ProtocolParseResult? _lastResult;
        private TelecommandParseResult? _lastTelecommandResult;

        internal ProtocolParserPanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            WireEvents();
        }

        public void OnActivated() { }

        private void WireEvents()
        {
            _browseBtn.Click += OnBrowse;
            _analyzeBtn.Click += OnAnalyze;
            _exportBtn.Click += OnExport;
            _downloadTemplateBtn.Click += OnDownloadTemplate;
            _openFolderBtn.Click += OnOpenFolder;
        }

        private void OnBrowse(object sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "Word 文档|*.docx|所有文件|*.*",
                Title = "选择协议文档"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _selectedFilePath = dlg.FileName;
            _filePathLabel.Text = Path.GetFileName(_selectedFilePath);
            _filePathLabel.ForeColor = System.Drawing.Color.Black;
            _analyzeBtn.Enabled = true;
            _exportBtn.Enabled = false;
            _lastResult = null;
            _lastTelecommandResult = null;
            _previewBox.Clear();
            _resultBox.Clear();
        }

        private async void OnAnalyze(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedFilePath)) return;

            SetBusy(true, "正在分析协议文档...");
            try
            {
                if (GetTemplateType() == ProtocolTemplateType.Telemetry)
                {
                    _lastResult = await Task.Run(() => _service.Analyze(_selectedFilePath));
                    _lastTelecommandResult = null;
                    ShowPreview(_lastResult);
                    ApplyDetectedSettings(_lastResult.SystemName);
                    _ctx.NotifyStatus($"协议分析完成：同步 {_lastResult.SyncFieldCount} 字段，异步 {_lastResult.AsyncFieldCount} 字段");
                }
                else
                {
                    _lastTelecommandResult = await Task.Run(() => _service.AnalyzeTelecommand(_selectedFilePath));
                    _lastResult = null;
                    ShowPreview(_lastTelecommandResult);
                    ApplyDetectedSettings(_lastTelecommandResult.SystemName);
                    _ctx.NotifyStatus($"遥控协议分析完成：检测 {_lastTelecommandResult.Commands.Count} 条指令");
                }

                _exportBtn.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"分析失败：{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async void OnExport(object sender, EventArgs e)
        {
            if (_lastResult == null && _lastTelecommandResult == null) return;

            using var dlg = new FolderBrowserDialog
            {
                Description = "选择输出目录"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            _lastOutputDir = dlg.SelectedPath;
            SetBusy(true, "正在生成 Excel 配置...");

            try
            {
                List<string> files;
                if (GetTemplateType() == ProtocolTemplateType.Telemetry)
                {
                    if (_lastResult == null) return;
                    var options = BuildExportOptions();
                    OverrideResultSettings(_lastResult, options);
                    files = await Task.Run(() => _service.Export(_lastResult, _lastOutputDir, options));
                }
                else
                {
                    if (_lastTelecommandResult == null) return;
                    var options = BuildTelecommandExportOptions();
                    OverrideResultSettings(_lastTelecommandResult);
                    files = await Task.Run(() => _service.ExportTelecommand(_lastTelecommandResult, _lastOutputDir, options));
                }

                ShowExportResult(files);
                _openFolderBtn.Enabled = true;
                MessageHelper.Success(this, $"已生成配置文件：{string.Join("，", files.Select(Path.GetFileName))}");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"导出失败：{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void OnDownloadTemplate(object sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Filter = "Excel 文件|*.xlsx",
                FileName = "遥测解析配置模板.xlsx",
                Title = "保存模板文件"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                _service.GenerateTemplate(dlg.FileName);
                MessageHelper.Success(this, "模板已保存");
            }
            catch (Exception ex)
            {
                MessageHelper.Error(this, $"模板生成失败：{ex.Message}");
            }
        }

        private void OnOpenFolder(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_lastOutputDir) && Directory.Exists(_lastOutputDir))
                Process.Start("explorer.exe", _lastOutputDir);
        }

        private void ShowPreview(ProtocolParseResult result)
        {
            _previewBox.Clear();
            AppendPreview($"文档标题: {result.DocumentTitle}");
            AppendPreview($"系统名称: {result.SystemName}");
            AppendPreview($"默认端序: {result.DefaultEndianness}");
            AppendPreview("");

            AppendPreview($"━━ 同步遥测 ({result.SyncTables.Count} 表, {result.SyncFieldCount} 字段) ━━");
            foreach (var t in result.SyncTables)
            {
                AppendPreview($"  表: {t.TableTitle}  [章节: {t.SectionHeading}]");
                ShowFieldSummary(t.Fields, "  ");
            }

            AppendPreview("");
            AppendPreview($"━━ 异步遥测 ({result.AsyncTables.Count} 表, {result.AsyncFieldCount} 字段) ━━");
            foreach (var t in result.AsyncTables)
            {
                AppendPreview($"  表: {t.TableTitle}  [章节: {t.SectionHeading}]");
                ShowFieldSummary(t.Fields, "  ");
            }

            if (result.SyncChannels.Count > 0 || result.AsyncChannels.Count > 0)
            {
                AppendPreview("");
                AppendPreview("━━ 通道信息 ━━");
                foreach (var ch in result.SyncChannels)
                    AppendPreview($"  同步 {ch.ChannelLabel}: APID={ch.FrameIdHex}");
                foreach (var ch in result.AsyncChannels)
                    AppendPreview($"  异步 {ch.ChannelLabel}: APID={ch.FrameIdHex}");
            }

            if (result.Warnings.Count > 0)
            {
                AppendPreview("");
                AppendPreview("━━ 警告 ━━");
                foreach (string w in result.Warnings)
                    AppendPreview($"  ⚠ {w}");
            }
        }

        private void ShowPreview(TelecommandParseResult result)
        {
            _previewBox.Clear();
            AppendPreview($"文档标题: {result.DocumentTitle}");
            AppendPreview($"系统名称: {result.SystemName}");
            AppendPreview($"默认端序: {result.DefaultEndianness}");
            AppendPreview("");
            AppendPreview($"━━ 遥控指令 ({result.Commands.Count} 条) ━━");

            int showCount = Math.Min(20, result.Commands.Count);
            for (int i = 0; i < showCount; i++)
            {
                TelecommandEntry c = result.Commands[i];
                AppendPreview($"  {c.Code} | {c.Name} | 代号:{c.CodeAlias} | 参数模板:{c.Presets.Count}");
            }
            if (result.Commands.Count > showCount)
                AppendPreview($"  ... 共 {result.Commands.Count} 条指令");

            if (result.FrameInfos.Count > 0)
            {
                AppendPreview("");
                AppendPreview("━━ CAN 帧头规则 ━━");
                foreach (CanFrameInfo fi in result.FrameInfos.Take(10))
                    AppendPreview($"  {fi.Channel} | {fi.FrameType} | {BitConverter.ToString(fi.HeaderBytes).Replace("-", " ")}");
            }

            if (result.Warnings.Count > 0)
            {
                AppendPreview("");
                AppendPreview("━━ 警告 ━━");
                foreach (string w in result.Warnings)
                    AppendPreview($"  ⚠ {w}");
            }
        }

        private void ShowFieldSummary(List<ProtocolTelemetryField> fields, string indent)
        {
            int dataFields = fields.Count(f => !f.IsHeaderField && !f.IsChecksum && !f.IsReserved);
            int bitFields = fields.Count(f => f.BitLength > 0);
            int withUnit = fields.Count(f => !string.IsNullOrEmpty(f.Unit));
            int withEnum = fields.Count(f => !string.IsNullOrEmpty(f.EnumMapping));

            AppendPreview($"{indent}  数据字段: {dataFields}, 位字段: {bitFields}, 带单位: {withUnit}, 带枚举: {withEnum}");

            int showCount = Math.Min(5, fields.Count);
            for (int i = 0; i < showCount; i++)
            {
                var f = fields[i];
                string info = $"{indent}    {f.ByteSequence} | {f.FieldName}";
                if (f.BitLength > 0) info += $" [bit{f.BitOffset}:{f.BitLength}b]";
                if (!string.IsNullOrEmpty(f.Unit)) info += $" ({f.Unit})";
                AppendPreview(info);
            }
            if (fields.Count > showCount)
                AppendPreview($"{indent}    ... 共 {fields.Count} 个字段");
        }

        private void ShowExportResult(List<string> files)
        {
            _resultBox.Clear();
            AppendResult("导出完成！生成的文件：");
            AppendResult("");
            foreach (string f in files)
                AppendResult($"  {Path.GetFileName(f)}");
            AppendResult("");
            AppendResult($"输出目录: {_lastOutputDir}");
        }

        private void ApplyDetectedSettings(string systemName)
        {
            if (string.IsNullOrEmpty(_systemNameInput.Text))
                _systemNameInput.PlaceholderText = systemName;
        }

        private ExportOptions BuildExportOptions()
        {
            string? prefix = _codePrefixInput.Text?.Trim();
            if (string.IsNullOrEmpty(prefix))
                prefix = _systemNameInput.Text?.Trim();

            return new ExportOptions
            {
                TelemetryCodePrefix = string.IsNullOrEmpty(prefix) ? null : prefix,
                DefaultFormulaType = _formulaTypeInput.Text?.Trim() ?? "5",
                DefaultFormulaCoeff = _formulaCoeffInput.Text?.Trim() ?? "1/0/",
                IncludeHeaderFields = _includeHeaderCheck.Checked,
                IncludeChecksum = _includeChecksumCheck.Checked
            };
        }

        private TelecommandExportOptions BuildTelecommandExportOptions()
        {
            var options = new TelecommandExportOptions
            {
                CodePrefixOverride = string.IsNullOrWhiteSpace(_codePrefixInput.Text) ? null : _codePrefixInput.Text.Trim(),
                Formats = GetExportFormat(),
                IncludeUsageSheet = true
            };
            return options;
        }

        private void OverrideResultSettings(ProtocolParseResult result, ExportOptions options)
        {
            string sysOverride = (_systemNameInput.Text ?? "").Trim();
            if (sysOverride.Length > 0)
                result.SystemName = sysOverride;
        }

        private void OverrideResultSettings(TelecommandParseResult result)
        {
            string sysOverride = (_systemNameInput.Text ?? "").Trim();
            if (sysOverride.Length > 0)
                result.SystemName = sysOverride;
        }

        private void SetBusy(bool busy, string status = "")
        {
            _browseBtn.Enabled = !busy;
            _analyzeBtn.Enabled = !busy && !string.IsNullOrEmpty(_selectedFilePath);
            _exportBtn.Enabled = !busy && (_lastResult != null || _lastTelecommandResult != null);
            _downloadTemplateBtn.Enabled = !busy;

            if (!string.IsNullOrEmpty(status))
                _ctx.NotifyStatus(status);
            else if (!busy)
                _ctx.NotifyStatus("就绪");
        }

        private void AppendPreview(string text)
        {
            _previewBox.AppendText(text + Environment.NewLine);
        }

        private void AppendResult(string text)
        {
            _resultBox.AppendText(text + Environment.NewLine);
        }

        private ProtocolTemplateType GetTemplateType()
        {
            string text = _templateTypeCombo.SelectedItem?.ToString() ?? "";
            return text.Contains("遥控") ? ProtocolTemplateType.Telecommand : ProtocolTemplateType.Telemetry;
        }

        private TelecommandExportFormat GetExportFormat()
        {
            string text = _exportFormatCombo.SelectedItem?.ToString() ?? "";
            if (text.Contains("仅格式A")) return TelecommandExportFormat.FormatA;
            if (text.Contains("仅格式B")) return TelecommandExportFormat.FormatB;
            return TelecommandExportFormat.Both;
        }
    }
}
