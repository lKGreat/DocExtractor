using System;
using System.IO;
using Microsoft.ML;

namespace DocExtractor.ML.ColumnClassifier
{
    /// <summary>
    /// 列名分类器推理封装
    /// 加载已训练的 ML.NET 模型，提供线程安全的预测接口
    /// </summary>
    public class ColumnClassifierModel : IDisposable
    {
        private readonly MLContext _mlContext;
        private ITransformer? _model;
        private PredictionEngine<ColumnInput, ColumnPrediction>? _engine;
        private readonly object _lock = new object();

        public bool IsLoaded => _model != null;

        public ColumnClassifierModel()
        {
            _mlContext = new MLContext(seed: 42);
        }

        /// <summary>从 .zip 文件加载已训练模型</summary>
        public void Load(string modelPath)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException($"模型文件不存在: {modelPath}");

            lock (_lock)
            {
                _engine?.Dispose();
                _model = _mlContext.Model.Load(modelPath, out _);
                _engine = _mlContext.Model.CreatePredictionEngine<ColumnInput, ColumnPrediction>(_model);
            }
        }

        /// <summary>
        /// 预测列名对应的规范字段名
        /// </summary>
        /// <returns>(规范字段名, 置信度) 或 (null, 0) 表示未能分类</returns>
        public (string? FieldName, float Confidence) Predict(string columnText)
        {
            if (!IsLoaded || string.IsNullOrWhiteSpace(columnText))
                return (null, 0f);

            lock (_lock)
            {
                var input = new ColumnInput { ColumnText = columnText };
                var prediction = _engine!.Predict(input);

                float maxScore = 0f;
                if (prediction.Score != null)
                {
                    foreach (var s in prediction.Score)
                        if (s > maxScore) maxScore = s;
                }

                return (prediction.PredictedLabel, maxScore);
            }
        }

        /// <summary>热重载模型（训练完成后无需重启）</summary>
        public void Reload(string modelPath) => Load(modelPath);

        public void Dispose()
        {
            _engine?.Dispose();
        }
    }
}
