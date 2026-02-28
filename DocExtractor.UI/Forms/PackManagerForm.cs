using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Core.Models;
using DocExtractor.Core.Packs;
using DocExtractor.Data.Packs;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Forms
{
    public partial class PackManagerForm : Form
    {
        private readonly string _dbPath;
        private readonly Func<ExtractionConfig?> _getCurrentConfig;
        private readonly string _packsRoot;

        public PackManagerForm(string dbPath, Func<ExtractionConfig?> getCurrentConfig)
        {
            _dbPath = dbPath;
            _getCurrentConfig = getCurrentConfig;
            _packsRoot = Path.Combine(Application.StartupPath, "packs");
            Directory.CreateDirectory(_packsRoot);

            InitializeComponent();
            WireEvents();
            RefreshInstalledList();
        }

        private void WireEvents()
        {
            _refreshBtn.Click += (_, _) => RefreshInstalledList();
            _installBtn.Click += (_, _) => InstallPack();
            _exportBtn.Click += (_, _) => ExportCurrentConfigAsPack();
        }

        private void RefreshInstalledList()
        {
            _installedListBox.Items.Clear();
            if (!Directory.Exists(_packsRoot))
                return;

            foreach (var dir in Directory.GetDirectories(_packsRoot).OrderBy(x => x))
                _installedListBox.Items.Add(Path.GetFileName(dir));

            AppendLog($"已加载 {_installedListBox.Items.Count} 个已安装配置包");
        }

        private void InstallPack()
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "DocExtractor 配置包|*.dxpack|ZIP 文件|*.zip|所有文件|*.*",
                Title = "选择要安装的配置包"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                var installer = new PackInstaller(_dbPath, _packsRoot);
                var result = installer.Install(dlg.FileName);
                AppendLog($"安装成功：{result.Manifest.Name}({result.Manifest.PackId})，导入配置 {result.ImportedConfigCount} 个");
                RefreshInstalledList();
                MessageHelper.Success(this, $"配置包安装成功：{result.Manifest.Name}");
            }
            catch (Exception ex)
            {
                AppendLog($"安装失败：{ex.Message}");
                MessageHelper.Error(this, $"安装失败：{ex.Message}");
            }
        }

        private void ExportCurrentConfigAsPack()
        {
            var config = _getCurrentConfig();
            if (config == null || config.Fields.Count == 0)
            {
                MessageHelper.Warn(this, "当前配置为空，无法导出");
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Filter = "DocExtractor 配置包|*.dxpack",
                FileName = $"{config.ConfigName}.dxpack"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                string packId = BuildPackId(config.ConfigName);
                var manifest = new PackManifest
                {
                    PackId = packId,
                    Name = config.ConfigName,
                    Version = "1.0.0",
                    Author = "DocExtractor User",
                    Domain = "custom",
                    Description = $"由配置「{config.ConfigName}」导出"
                };

                var exporter = new PackExporter();
                exporter.Export(dlg.FileName, manifest, new[] { config });
                AppendLog($"导出成功：{dlg.FileName}");
                MessageHelper.Success(this, "配置包导出成功");
            }
            catch (Exception ex)
            {
                AppendLog($"导出失败：{ex.Message}");
                MessageHelper.Error(this, $"导出失败：{ex.Message}");
            }
        }

        private static string BuildPackId(string configName)
        {
            var chars = configName
                .ToLowerInvariant()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
                .ToArray();
            return new string(chars).Trim('-');
        }

        private void AppendLog(string message)
        {
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
            _logBox.ScrollToCaret();
        }
    }
}
