using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DocExtractor.Core.Models;

namespace DocExtractor.UI.Forms
{
    /// <summary>
    /// Field selection dialog for export. Replaces inline new Form() usage in MainForm.
    /// </summary>
    public partial class ExportFieldSelectionForm : Form
    {
        private readonly IReadOnlyList<FieldDefinition> _fields;

        public List<string> SelectedFieldNames { get; private set; } = new List<string>();

        public ExportFieldSelectionForm(IReadOnlyList<FieldDefinition> fields)
        {
            _fields = fields;
            InitializeComponent();
            PopulateFields();
        }

        private void PopulateFields()
        {
            foreach (var field in _fields)
            {
                string display = string.IsNullOrWhiteSpace(field.DisplayName)
                    ? field.FieldName
                    : $"{field.DisplayName} ({field.FieldName})";
                _fieldList.Items.Add(display, true);
            }
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            for (int i = 0; i < _fieldList.Items.Count; i++)
                _fieldList.SetItemChecked(i, true);
        }

        private void OnClearAll(object sender, EventArgs e)
        {
            for (int i = 0; i < _fieldList.Items.Count; i++)
                _fieldList.SetItemChecked(i, false);
        }

        private void OnConfirm(object sender, EventArgs e)
        {
            SelectedFieldNames = new List<string>();
            for (int i = 0; i < _fieldList.Items.Count; i++)
            {
                if (_fieldList.GetItemChecked(i))
                    SelectedFieldNames.Add(_fields[i].FieldName);
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
