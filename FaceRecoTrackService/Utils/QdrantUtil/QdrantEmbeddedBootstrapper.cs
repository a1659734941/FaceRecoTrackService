using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FaceRecoTrackService.Utils.QdrantUtil
{
    public static class QdrantEmbeddedBootstrapper
    {
        public static async Task<Process?> EnsureStartedAsync(
            QdrantConfig config,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            var embedded = config.Embedded;
            if (embedded == null || !embedded.Enabled) return null;

            if (await IsPortOpenAsync(config.Host, config.Port, embedded.PortCheckTimeoutMs, cancellationToken))
            {
                logger.LogInformation("Qdrant已在 {Host}:{Port} 运行，跳过内置启动。", config.Host, config.Port);
                return null;
            }

            var baseDir = AppContext.BaseDirectory;
            var binPath = ResolvePath(embedded.BinPath, baseDir);
            if (!File.Exists(binPath))
            {
                throw new FileNotFoundException("未找到内置Qdrant可执行文件", binPath);
            }

            var workDir = ResolvePath(embedded.WorkingDirectory, baseDir);
            Directory.CreateDirectory(workDir);

            var processInfo = new ProcessStartInfo
            {
                FileName = binPath,
                Arguments = embedded.Arguments ?? string.Empty,
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(processInfo);
            if (process == null)
            {
                throw new InvalidOperationException("启动Qdrant失败");
            }

            var timeout = TimeSpan.FromSeconds(Math.Max(1, embedded.StartupTimeoutSeconds));
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed < timeout && !cancellationToken.IsCancellationRequested)
            {
                if (await IsPortOpenAsync(config.Host, config.Port, embedded.PortCheckTimeoutMs, cancellationToken))
                {
                    logger.LogInformation("Qdrant启动完成，PID: {Pid}", process.Id);
                    return process;
                }

                if (process.HasExited)
                {
                    throw new InvalidOperationException($"Qdrant启动后退出，退出码: {process.ExitCode}");
                }

                await Task.Delay(300, cancellationToken);
            }

            throw new TimeoutException("Qdrant启动超时");
        }

        private static async Task<bool> IsPortOpenAsync(string host, int port, int timeoutMs, CancellationToken cancellationToken)
        {
            try
            {
                using var tcp = new TcpClient();
                var connectTask = tcp.ConnectAsync(host, port);
                var timeoutTask = Task.Delay(Math.Max(50, timeoutMs), cancellationToken);
                var completed = await Task.WhenAny(connectTask, timeoutTask);
                return completed == connectTask && tcp.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static string ResolvePath(string path, string baseDir)
        {
            if (string.IsNullOrWhiteSpace(path)) return baseDir;
            return Path.IsPathRooted(path) ? path : Path.Combine(baseDir, path);
        }
    }
}
