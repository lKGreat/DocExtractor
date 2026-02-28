using System;
using System.IO;
using Microsoft.ML;

namespace DocExtractor.ML.SectionClassifier
{
    /// <summary>
    /// 章节标题二分类模型推理封装
    /// 加载已训练的 ML.NET 模型，提供线程安全的预测接口
    /// </summary>
    public class SectionClassifierModel : IDisposable
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<SectionInput, SectionPrediction>? _engine;
        private readonly object _lock = new object();

        public bool IsLoaded => _model != null;

        public SectionClassifierModel()
        {
            _mlContext = new MLContext(seed: 42);
        }

        /// <summary>从 .zip 文件加载已训练模型</summary>
        public void Load(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"章节分类模型文件不存在: {modelPath}");

            lock (_lock)
            {
                _engine?.Dispose();
                _model = _mlContext.Model.Load(modelPath, out _);
                _engine = _mlContext.Model.CreatePredictionEngine<SectionInput, SectionPrediction>(_model);
            }
        }

        /// <summary>热重载模型（训练完成后无需重启）</summary>
        public void Reload(string modelPath) => Load(modelPath);

        /// <summary>
        /// 预测段落是否为章节标题
        /// </summary>
        /// <returns>(IsHeading, Probability)；模型未加载时返回 (false, 0)</returns>
        public (bool IsHeading, float Probability) Predict(SectionInput input)
        {
            if (!IsLoaded) return (false, 0f);

            lock (_lock)
            {
                var prediction = _engine!.Predict(input);
                return (prediction.IsHeading, prediction.Probability);
            }
        }

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}
