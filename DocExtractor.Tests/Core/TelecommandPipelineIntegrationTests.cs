using System;
using System.IO;
using System.Linq;
using DocExtractor.Core.Protocol;
using DocExtractor.Data.Export;
using DocExtractor.Parsing.Word;
using OfficeOpenXml;
using Xunit;

namespace DocExtractor.Tests.Core
{
    public class TelecommandPipelineIntegrationTests
    {
        [Fact]
        public void AnalyzeAndExport_Telecommand_ShouldGenerateBothExcelFormats()
        {
            // Arrange
            string repoRoot = FindRepoRoot();
            string docxPath = Path.Combine(
                repoRoot,
                "docs",
                "sucai",
                "1_11-GMS GEN1卫星霍尔电推进组件CAN总线通信协议V4.2-20250613-1.5代的05组.docx");
            Assert.True(File.Exists(docxPath), "测试文档不存在: " + docxPath);

            var parser = new WordDocumentParser();
            var analyzer = new TelecommandAnalyzer();
            var exporter = new TelecommandConfigExporter();

            // Act
            var tables = parser.Parse(docxPath);
            TelecommandParseResult result = analyzer.Analyze(tables, Path.GetFileNameWithoutExtension(docxPath));

            string outputDir = Path.Combine(Path.GetTempPath(), "DocExtractor_TelecommandTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDir);
            var files = exporter.Export(result, outputDir, new TelecommandExportOptions
            {
                Formats = TelecommandExportFormat.Both
            });

            // Assert
            Assert.True(result.Commands.Count > 0);
            Assert.True(files.Count == 2);
            Assert.Contains(files, f => f.EndsWith("_遥控指令配置表.xlsx", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(files, f => f.EndsWith("_TelecommandConfig.xlsx", StringComparison.OrdinalIgnoreCase));
            Assert.All(files, f => Assert.True(File.Exists(f)));

            string formatA = files.First(f => f.EndsWith("_遥控指令配置表.xlsx", StringComparison.OrdinalIgnoreCase));
            string formatB = files.First(f => f.EndsWith("_TelecommandConfig.xlsx", StringComparison.OrdinalIgnoreCase));

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using (var pkg = new ExcelPackage(new FileInfo(formatA)))
            {
                Assert.NotNull(pkg.Workbook.Worksheets["更新记录"]);
                Assert.NotNull(pkg.Workbook.Worksheets["指令配置-A通道"]);
                Assert.NotNull(pkg.Workbook.Worksheets["指令配置-B通道"]);
            }

            using (var pkg = new ExcelPackage(new FileInfo(formatB)))
            {
                Assert.NotNull(pkg.Workbook.Worksheets["全局参数"]);
                Assert.NotNull(pkg.Workbook.Worksheets["指令配置"]);
            }
        }

        private static string FindRepoRoot()
        {
            string current = AppContext.BaseDirectory;
            var dir = new DirectoryInfo(current);
            while (dir != null)
            {
                string sln = Path.Combine(dir.FullName, "DocExtractor.sln");
                if (File.Exists(sln))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException("未找到仓库根目录（DocExtractor.sln）。");
        }
    }
}
