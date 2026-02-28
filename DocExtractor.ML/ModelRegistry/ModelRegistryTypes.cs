using System.Collections.Generic;

namespace DocExtractor.ML.ModelRegistry
{
    public class ModelRegistryEntry
    {
        public string Current { get; set; } = string.Empty;
        public List<ModelVersionInfo> Versions { get; set; } = new List<ModelVersionInfo>();
    }

    public class ModelVersionInfo
    {
        public string Version { get; set; } = string.Empty;
        public double Accuracy { get; set; }
        public int Samples { get; set; }
        public string TrainedAt { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
    }
}
