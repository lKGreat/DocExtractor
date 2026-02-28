using System;
using System.IO;
using System.Windows.Forms;
using DocExtractor.Data.Export;

namespace DocExtractor.UI
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 启动时自动生成训练数据模板（若不存在）
            try
            {
                string templateDir = Path.Combine(Application.StartupPath, "templates");
                TemplateGenerator.EnsureTemplates(templateDir);
            }
            catch
            {
                // 模板生成失败不阻塞启动
            }

            Application.Run(new Forms.MainForm());
        }
    }
}
