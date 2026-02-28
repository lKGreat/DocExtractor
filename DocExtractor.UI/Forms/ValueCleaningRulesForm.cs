using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DocExtractor.Core.Normalization;

namespace DocExtractor.UI.Forms
{
    public partial class ValueCleaningRulesForm : Form
    {
        public List<ValueCleaningRule> ResultRules { get; private set; }

        public ValueCleaningRulesForm(List<ValueCleaningRule> rules)
        {
            InitializeComponent();
            ResultRules = rules;
            LoadRules(rules);
            WireEvents();
        }

        private void WireEvents()
        {
            _okBtn.Click += OnOk;
            _cancelBtn.Click += OnCancel;
            _selectAllBtn.Click += OnSelectAll;
            _deselectAllBtn.Click += OnDeselectAll;
        }

        private void LoadRules(List<ValueCleaningRule> rules)
        {
            _rulesGrid.Rows.Clear();
            foreach (var rule in rules)
            {
                _rulesGrid.Rows.Add(rule.IsEnabled, rule.DisplayName, rule.Description, rule.Example);
            }
        }

        private void OnOk(object sender, EventArgs e)
        {
            for (int i = 0; i < _rulesGrid.Rows.Count && i < ResultRules.Count; i++)
            {
                var row = _rulesGrid.Rows[i];
                ResultRules[i].IsEnabled = row.Cells["Enabled"].Value is true;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void OnSelectAll(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in _rulesGrid.Rows)
                row.Cells["Enabled"].Value = true;
        }

        private void OnDeselectAll(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in _rulesGrid.Rows)
                row.Cells["Enabled"].Value = false;
        }
    }
}
