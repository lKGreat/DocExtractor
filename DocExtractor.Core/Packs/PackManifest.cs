namespace DocExtractor.Core.Packs
{
    /// <summary>
    /// 领域配置包元数据。
    /// </summary>
    public class PackManifest
    {
        public string PackId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ConfigCount { get; set; }
        public bool HasPretrainedModel { get; set; }
        public string MinAppVersion { get; set; } = "1.0.0";
    }
}
