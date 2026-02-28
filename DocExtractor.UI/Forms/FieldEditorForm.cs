using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DocExtractor.Core.Models;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Forms
{
    public partial class FieldEditorForm : Form
    {
        public FieldDefinition? EditedField { get; private set; }

        public FieldEditorForm(FieldDefinition initial)
        {
            InitializeComponent();
            _dataTypeCombo.Items.AddRange(Enum.GetNames(typeof(FieldDataType)));
            LoadField(initial);
        }

        private void LoadField(FieldDefinition field)
        {
            _fieldNameInput.Text = field.FieldName;
            _displayNameInput.Text = field.DisplayName;
            _dataTypeCombo.SelectedItem = field.DataType.ToString();
            _isRequiredCheck.Checked = field.IsRequired;
            _defaultValueInput.Text = field.DefaultValue ?? string.Empty;
            _variantsInput.Lines = field.KnownColumnVariants.ToArray();
        }

        private void OnConfirm(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_fieldNameInput.Text))
            {
                MessageHelper.Warn(this, "字段名不能为空");
                return;
            }

            var field = new FieldDefinition
            {
                FieldName = _fieldNameInput.Text.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(_displayNameInput.Text)
                    ? _fieldNameInput.Text.Trim()
                    : _displayNameInput.Text.Trim(),
                IsRequired = _isRequiredCheck.Checked,
                DefaultValue = string.IsNullOrWhiteSpace(_defaultValueInput.Text) ? null : _defaultValueInput.Text.Trim()
            };

            if (Enum.TryParse<FieldDataType>(_dataTypeCombo.SelectedItem?.ToString(), out var dt))
                field.DataType = dt;

            field.KnownColumnVariants = _variantsInput.Lines
                .Select(v => v.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            EditedField = field;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
