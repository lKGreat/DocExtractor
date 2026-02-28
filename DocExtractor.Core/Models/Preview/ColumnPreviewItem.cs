namespace DocExtractor.Core.Models.Preview
{
    /// <summary>
    /// 列映射预览项（原始列名 -> 规范字段）。
    /// </summary>
    public class ColumnPreviewItem
    {
        public int ColumnIndex { get; set; }
        public string RawColumnName { get; set; } = string.Empty;
        public string? MappedFieldName { get; set; }
        public string? MappedDisplayName { get; set; }
        public float Confidence { get; set; }
        public string MatchMethod { get; set; } = string.Empty;

        public bool IsLowConfidence =>
            string.IsNullOrWhiteSpace(MappedFieldName) || Confidence < 0.75f;
    }
}
