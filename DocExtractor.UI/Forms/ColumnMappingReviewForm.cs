using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Core.Models;
using DocExtractor.Core.Models.Preview;

namespace DocExtractor.UI.Forms
{
    public partial class ColumnMappingReviewForm : Form
    {
        private readonly IReadOnlyList<FieldDefinition> _fields;
        private readonly IReadOnlyList<ColumnPreviewItem> _items;

        public List<(string RawColumn, string FieldName)> Corrections { get; } =
            new List<(string RawColumn, string FieldName)>();

        public ColumnMappingReviewForm(
            IReadOnlyList<ColumnPreviewItem> items,
            IReadOnlyList<FieldDefinition> fields)
        {
            _items = items;
            _fields = fields;
            InitializeComponent();
            BuildGrid();
        }

        private void BuildGrid()
        {
            _grid.Rows.Clear();
            var options = _fields
                .Select(f => string.IsNullOrWhiteSpace(f.DisplayName) ? f.FieldName : $"{f.DisplayName} ({f.FieldName})")
                .ToList();

            foreach (var item in _items)
            {
                int rowIndex = _grid.Rows.Add(
                    item.RawColumnName,
                    item.MappedFieldName ?? "未匹配",
                    $"{item.Confidence:P0}",
                    string.Empty);

                var comboCell = (DataGridViewComboBoxCell)_grid.Rows[rowIndex].Cells["CorrectField"];
                foreach (var opt in options)
                    comboCell.Items.Add(opt);

                if (!string.IsNullOrWhiteSpace(item.MappedFieldName))
                {
                    var current = _fields.FirstOrDefault(f => f.FieldName == item.MappedFieldName);
                    if (current != null)
                    {
                        string display = string.IsNullOrWhiteSpace(current.DisplayName)
                            ? current.FieldName
                            : $"{current.DisplayName} ({current.FieldName})";
                        comboCell.Value = display;
                    }
                }
            }
        }

        private void OnConfirm(object sender, EventArgs e)
        {
            Corrections.Clear();
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.IsNewRow) continue;
                string rawColumn = row.Cells["RawColumn"].Value?.ToString() ?? string.Empty;
                string selected = row.Cells["CorrectField"].Value?.ToString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(rawColumn) || string.IsNullOrWhiteSpace(selected))
                    continue;

                string fieldName = ExtractFieldName(selected);
                if (string.IsNullOrWhiteSpace(fieldName))
                    continue;

                Corrections.Add((rawColumn, fieldName));
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private static string ExtractFieldName(string selected)
        {
            int left = selected.LastIndexOf('(');
            int right = selected.LastIndexOf(')');
            if (left >= 0 && right > left)
                return selected.Substring(left + 1, right - left - 1).Trim();
            return selected.Trim();
        }
    }
}
