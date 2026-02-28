using System.Windows.Forms;

namespace DocExtractor.UI.Helpers
{
    /// <summary>
    /// AntdUI 消息通知封装，替代 MessageBox.Show。
    /// 接受 Control 或 Form — UserControl 传 this 即可。
    /// </summary>
    internal static class MessageHelper
    {
        private static Form ResolveForm(Control owner) => (owner as Form) ?? owner.FindForm();

        /// <summary>信息提示（自动消失）</summary>
        public static void Info(Control owner, string text) =>
            AntdUI.Message.info(ResolveForm(owner), text, autoClose: 3);

        /// <summary>成功提示（自动消失）</summary>
        public static void Success(Control owner, string text) =>
            AntdUI.Message.success(ResolveForm(owner), text, autoClose: 3);

        /// <summary>警告提示（自动消失）</summary>
        public static void Warn(Control owner, string text) =>
            AntdUI.Message.warn(ResolveForm(owner), text, autoClose: 5);

        /// <summary>错误提示（停留较久）</summary>
        public static void Error(Control owner, string text) =>
            AntdUI.Message.error(ResolveForm(owner), text, autoClose: 8);
    }
}
