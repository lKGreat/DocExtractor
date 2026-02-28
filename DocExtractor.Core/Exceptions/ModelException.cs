using System;

namespace DocExtractor.Core.Exceptions
{
    /// <summary>
    /// 模型异常：模型文件损坏、版本不兼容、推理失败等。
    /// </summary>
    public class ModelException : DocExtractorException
    {
        public string? ModelName { get; }
        public string? ModelPath { get; }

        public ModelException(
            string message,
            string? modelName = null,
            string? modelPath = null,
            Exception? innerException = null)
            : base(message, "Model", "MODEL_ERROR", innerException)
        {
            ModelName = modelName;
            ModelPath = modelPath;
        }
    }
}
