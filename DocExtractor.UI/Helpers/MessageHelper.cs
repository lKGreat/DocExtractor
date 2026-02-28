using System.Windows.Forms;

namespace DocExtractor.UI.Helpers
{
    /// <summary>
    /// AntdUI 消息通知封装，替代 MessageBox.Show
    /// </summary>
    internal static class MessageHelper
    {
        /// <summary>信息提示（自动消失）</summary>
        public static void Info(Form owner, string text)
        {
            AntdUI.Message.info(owner, text, autoClose: 3);
        }

        /// <summary>成功提示（自动消失）</summary>
        public static void Success(Form owner, string text)
        {
            AntdUI.Message.success(owner, text, autoClose: 3);
        }

        /// <summary>警告提示（自动消失）</summary>
        public static void Warn(Form owner, string text)
        {
            AntdUI.Message.warn(owner, text, autoClose: 5);
        }

        /// <summary>错误提示（停留较久）</summary>
        public static void Error(Form owner, string text)
        {
            AntdUI.Message.error(owner, text, autoClose: 8);
        }
    }
}
