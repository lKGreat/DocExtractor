using System.Collections.Generic;
using System.Windows.Forms;
using DocExtractor.UI.Services;

namespace DocExtractor.UI.Forms
{
    internal partial class ColumnErrorAnalysisForm : Form
    {
        internal ColumnErrorAnalysisForm(IReadOnlyList<ColumnErrorAnalysisItem> items)
        {
            InitializeComponent();
            LoadItems(items);
        }

        private void LoadItems(IReadOnlyList<ColumnErrorAnalysisItem> items)
        {
            _grid.Rows.Clear();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                int row = _grid.Rows.Add(
                    (i + 1).ToString(),
                    item.RawColumnName,
                    item.PredictedField,
                    item.ActualField,
                    item.Confidence.ToString("P0"));

                if (item.Confidence >= 0.6f)
                    _grid.Rows[row].DefaultCellStyle.BackColor = System.Drawing.Color.FromArgb(255, 240, 240);
            }
        }
    }
}
