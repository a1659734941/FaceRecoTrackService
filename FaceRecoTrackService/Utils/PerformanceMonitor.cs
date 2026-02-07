using System.Diagnostics;
using System.Text.Json;
using Serilog;

namespace FaceRecoTrackService.Utils
{
    /// <summary>
    /// 性能监控类，用于跟踪和记录操作的执行时间和相关指标
    /// 实现了IDisposable接口，可使用using语句自动记录性能数据
    /// </summary>
    public class PerformanceMonitor : IDisposable
    {
        /// <summary>
        /// 秒表，用于测量操作执行时间
        /// </summary>
        private readonly Stopwatch _stopwatch;
        
        /// <summary>
        /// 操作名称，用于日志记录
        /// </summary>
        private readonly string _operationName;
        
        /// <summary>
        /// 指标字典，用于存储操作的相关指标
        /// </summary>
        private readonly Dictionary<string, object> _metrics = new Dictionary<string, object>();
        
        /// <summary>
        ///  dispose标记，防止重复dispose
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// 构造函数，初始化性能监控器
        /// </summary>
        /// <param name="operationName">操作名称</param>
        public PerformanceMonitor(string operationName)
        {
            _operationName = operationName;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// 添加自定义指标
        /// </summary>
        /// <param name="key">指标键</param>
        /// <param name="value">指标值</param>
        public void AddMetric(string key, object value)
        {
            _metrics[key] = value;
        }

        /// <summary>
        /// 添加人脸识别相关指标
        /// </summary>
        /// <param name="detectedCount">检测到的人脸数量</param>
        /// <param name="processedCount">处理的人脸数量</param>
        /// <param name="averageSharpness">平均清晰度</param>
        /// <param name="threshold">清晰度阈值</param>
        public void AddFaceMetrics(int detectedCount, int processedCount, double averageSharpness, double threshold)
        {
            AddMetric("detected_faces", detectedCount);
            AddMetric("processed_faces", processedCount);
            AddMetric("average_sharpness", averageSharpness);
            AddMetric("sharpness_threshold", threshold);
        }

        /// <summary>
        /// 实现IDisposable接口
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 实际的dispose方法
        /// </summary>
        /// <param name="disposing">是否由用户代码调用</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _stopwatch.Stop();
                var elapsedMs = _stopwatch.ElapsedMilliseconds;
                
                // 构建日志数据
                var logData = new Dictionary<string, object>
                {
                    { "operation", _operationName },
                    { "duration_ms", elapsedMs }
                };

                // 添加所有自定义指标
                foreach (var metric in _metrics)
                {
                    logData[metric.Key] = metric.Value;
                }

                // 记录性能日志
                Log.Information("性能监控: {Operation} 耗时 {Duration}ms", _operationName, elapsedMs);
                Log.Debug("性能详情: {Details}", JsonSerializer.Serialize(logData, new JsonSerializerOptions { WriteIndented = true }));
            }

            _disposed = true;
        }

        /// <summary>
        /// 析构函数
        /// </summary>
        ~PerformanceMonitor()
        {
            Dispose(false);
        }
    }
}