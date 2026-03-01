using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace DocExtractor.UI.Controls
{
    internal static class NlpLabTheme
    {
        // ── Colors ──────────────────────────────────────────────────────────
        public static readonly Color Primary       = Color.FromArgb(24, 144, 255);   // #1890FF
        public static readonly Color PrimaryDark    = Color.FromArgb(16, 110, 205);
        public static readonly Color Success        = Color.FromArgb(82, 196, 26);    // #52C41A
        public static readonly Color Warning        = Color.FromArgb(250, 173, 20);   // #FAAD14
        public static readonly Color Danger         = Color.FromArgb(255, 77, 79);    // #FF4D4F
        public static readonly Color TextPrimary    = Color.FromArgb(38, 38, 38);
        public static readonly Color TextSecondary  = Color.FromArgb(89, 89, 89);
        public static readonly Color TextTertiary   = Color.FromArgb(140, 140, 140);
        public static readonly Color Border         = Color.FromArgb(217, 217, 217);
        public static readonly Color GridLine       = Color.FromArgb(230, 230, 230);
        public static readonly Color BgBody         = Color.FromArgb(245, 247, 250);
        public static readonly Color BgCard         = Color.White;
        public static readonly Color BgToolbar      = Color.White;
        public static readonly Color BgStatusBar    = Color.FromArgb(240, 240, 240);
        public static readonly Color BgInput        = Color.FromArgb(250, 250, 250);
        public static readonly Color NavBg          = Color.FromArgb(40, 44, 52);
        public static readonly Color NavFg          = Color.FromArgb(180, 180, 180);
        public static readonly Color NavActiveFg    = Color.White;

        // ── Fonts ───────────────────────────────────────────────────────────
        private static readonly string ResolvedFontFamily = ResolveFontFamily("微软雅黑", "Microsoft YaHei", "Segoe UI");
        private static readonly string ResolvedMonoFamily = ResolveFontFamily("Consolas", "Courier New", "Lucida Console");

        public static readonly Font Title         = SafeFont(ResolvedFontFamily, 11F, FontStyle.Bold);
        public static readonly Font SectionTitle   = SafeFont(ResolvedFontFamily, 9.5F, FontStyle.Bold);
        public static readonly Font Body           = SafeFont(ResolvedFontFamily, 9F);
        public static readonly Font BodyBold       = SafeFont(ResolvedFontFamily, 9F, FontStyle.Bold);
        public static readonly Font Small          = SafeFont(ResolvedFontFamily, 8.5F);
        public static readonly Font TextInput      = SafeFont(ResolvedFontFamily, 10F);
        public static readonly Font TextResult     = SafeFont(ResolvedFontFamily, 10.5F);
        public static readonly Font Mono           = SafeFont(ResolvedMonoFamily, 9F);

        private static string ResolveFontFamily(params string[] candidates)
        {
            using (var fonts = new InstalledFontCollection())
            {
                var installed = new System.Collections.Generic.HashSet<string>(
                    fonts.Families.Select(f => f.Name), StringComparer.OrdinalIgnoreCase);
                foreach (var name in candidates)
                {
                    if (installed.Contains(name)) return name;
                }
            }
            return SystemFonts.DefaultFont.FontFamily.Name;
        }

        private static Font SafeFont(string family, float size, FontStyle style = FontStyle.Regular)
        {
            try
            {
                var font = new Font(family, Math.Max(size, 1F), style);
                font.ToHfont();
                return font;
            }
            catch
            {
                return new Font(SystemFonts.DefaultFont.FontFamily, Math.Max(size, 1F), style);
            }
        }

        // ── Button Helpers ──────────────────────────────────────────────────

        public static Button MakePrimary(Button btn)
        {
            btn.BackColor  = Primary;
            btn.ForeColor  = Color.White;
            btn.FlatStyle  = FlatStyle.Flat;
            btn.Font       = Body;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        public static Button MakeSuccess(Button btn)
        {
            btn.BackColor  = Success;
            btn.ForeColor  = Color.White;
            btn.FlatStyle  = FlatStyle.Flat;
            btn.Font       = BodyBold;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        public static Button MakeDanger(Button btn)
        {
            btn.BackColor  = Danger;
            btn.ForeColor  = Color.White;
            btn.FlatStyle  = FlatStyle.Flat;
            btn.Font       = Body;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        public static Button MakeDefault(Button btn)
        {
            btn.BackColor  = BgCard;
            btn.ForeColor  = TextPrimary;
            btn.FlatStyle  = FlatStyle.Flat;
            btn.Font       = Small;
            btn.FlatAppearance.BorderColor = Border;
            btn.FlatAppearance.BorderSize  = 1;
            return btn;
        }

        public static Button MakeGhost(Button btn)
        {
            btn.BackColor  = Color.Transparent;
            btn.ForeColor  = TextSecondary;
            btn.FlatStyle  = FlatStyle.Flat;
            btn.Font       = Small;
            btn.FlatAppearance.BorderSize = 0;
            return btn;
        }

        // ── DataGridView Helper ─────────────────────────────────────────────

        public static void StyleGrid(DataGridView grid)
        {
            grid.AllowUserToAddRows    = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible     = false;
            grid.SelectionMode         = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect           = false;
            grid.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BorderStyle           = BorderStyle.None;
            grid.BackgroundColor       = BgCard;
            grid.GridColor             = GridLine;
            grid.Font                  = Body;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.ColumnHeadersHeight   = 32;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextSecondary;
            grid.ColumnHeadersDefaultCellStyle.Font      = Small;
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(230, 244, 255);
            grid.DefaultCellStyle.SelectionForeColor = TextPrimary;
        }

        // ── SplitContainer Helper ───────────────────────────────────────────

        public static void SetSplitterDistanceDeferred(
            SplitContainer split,
            double ratio,
            int? panel1Min = null,
            int? panel2Min = null)
        {
            split.HandleCreated += (s, e) =>
            {
                split.BeginInvoke((MethodInvoker)(() =>
                {
                    try
                    {
                        int totalSize = split.Orientation == Orientation.Horizontal
                            ? split.Height
                            : split.Width;
                        if (totalSize <= 0) return;

                        int desiredPanel1Min = panel1Min ?? split.Panel1MinSize;
                        int desiredPanel2Min = panel2Min ?? split.Panel2MinSize;

                        // Important: avoid setting impossible min sizes while control is still settling.
                        int maxPanel1Min = System.Math.Max(0, totalSize - desiredPanel2Min - split.SplitterWidth - 1);
                        int actualPanel1Min = System.Math.Min(desiredPanel1Min, maxPanel1Min);
                        int maxPanel2Min = System.Math.Max(0, totalSize - actualPanel1Min - split.SplitterWidth - 1);
                        int actualPanel2Min = System.Math.Min(desiredPanel2Min, maxPanel2Min);

                        split.Panel1MinSize = actualPanel1Min;
                        split.Panel2MinSize = actualPanel2Min;

                        int minDist = actualPanel1Min;
                        int maxDist = System.Math.Max(minDist, totalSize - actualPanel2Min - split.SplitterWidth);
                        int dist = (int)(totalSize * ratio);
                        dist = System.Math.Max(minDist, System.Math.Min(maxDist, dist));
                        split.SplitterDistance = dist;
                    }
                    catch { }
                }));
            };
        }
    }
}
