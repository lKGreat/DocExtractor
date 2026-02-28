using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using DocExtractor.Core.Models;
using DocExtractor.UI.Context;
using DocExtractor.UI.Helpers;

namespace DocExtractor.UI.Controls
{
    /// <summary>
    /// Split rules panel: editable grid of SplitRule definitions for the current config.
    /// </summary>
    public partial class SplitRulePanel : UserControl
    {
        private readonly DocExtractorContext _ctx;

        internal SplitRulePanel(DocExtractorContext ctx)
        {
            _ctx = ctx;
            InitializeComponent();
            _saveSplitBtn.Click += OnSaveSplitRules;
            _ctx.ConfigChanged += LoadRulesToGrid;
        }

        public void OnActivated() => LoadRulesToGrid();

        private void LoadRulesToGrid()
        {
            _splitGrid.Rows.Clear();
            foreach (var r in _ctx.CurrentConfig.SplitRules)
            {
                _splitGrid.Rows.Add(
                    r.RuleName,
                    r.Type.ToString(),
                    r.TriggerColumn,
                    string.Join(",", r.Delimiters),
                    r.GroupByColumn,
                    r.InheritParentFields,
                    r.Priority.ToString(),
                    r.IsEnabled);
            }
        }

        private void OnSaveSplitRules(object sender, EventArgs e)
        {
            _ctx.CurrentConfig.SplitRules.Clear();
            foreach (DataGridViewRow row in _splitGrid.Rows)
            {
                if (row.IsNewRow) continue;
                var rule = BuildRuleFromRow(row);
                if (rule != null) _ctx.CurrentConfig.SplitRules.Add(rule);
            }
            _ctx.Logger?.LogInformation("拆分规则已保存");
            MessageHelper.Success(this, "拆分规则已保存");
        }

        private static SplitRule BuildRuleFromRow(DataGridViewRow row)
        {
            var name = row.Cells["RuleName"].Value?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return null;

            var r = new SplitRule
            {
                RuleName = name,
                TriggerColumn = row.Cells["TriggerColumn"].Value?.ToString() ?? string.Empty,
                GroupByColumn = row.Cells["GroupByColumn"].Value?.ToString() ?? string.Empty,
                InheritParentFields = row.Cells["InheritParent"].Value is true,
                IsEnabled = row.Cells["Enabled"].Value is true || row.Cells["Enabled"].Value == null
            };

            if (Enum.TryParse<SplitType>(row.Cells["Type"].Value?.ToString(), out var st)) r.Type = st;

            var delimiters = row.Cells["Delimiters"].Value?.ToString() ?? "/;、";
            r.Delimiters = new List<string>(delimiters.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            if (int.TryParse(row.Cells["Priority"].Value?.ToString(), out int pri)) r.Priority = pri;

            return r;
        }
    }
}
