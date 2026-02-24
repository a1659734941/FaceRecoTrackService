using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FaceRecoTrackService.Core.Options;
using Serilog;

namespace FaceRecoTrackService.Infrastructure.External
{
    public class FolderSnapshotResult
    {
        public byte[] ImageBytes { get; set; } = Array.Empty<byte>();
        public DateTime CaptureTimeUtc { get; set; } = DateTime.UtcNow;
        public string FilePath { get; set; } = "";
        public string CameraIp { get; set; } = "";
        public string CameraName { get; set; } = "";
        public string Location { get; set; } = "";
        /// <summary>性别：1=男，0=女，-1=未解析</summary>
        public int Gender { get; set; } = -1;
    }

    public class FtpFolderSnapshotClient
    {
        private readonly Dictionary<string, (DateTime LastWriteUtc, long Length)> _processed =
            new Dictionary<string, (DateTime, long)>(StringComparer.OrdinalIgnoreCase);

        public async Task<List<FolderSnapshotResult>> FetchNewSnapshotsAsync(
            FtpFolderOptions options,
            CancellationToken cancellationToken)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            var results = new List<FolderSnapshotResult>();

            if (!Directory.Exists(options.Path))
            {
                Log.Warning("监听目录不存在：{Path}", options.Path);
                return results;
            }

            var searchOption = options.IncludeSubdirectories ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = new List<string>();
            foreach (var pattern in options.FilePatterns.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                files.AddRange(Directory.EnumerateFiles(options.Path, pattern, searchOption));
            }

            // 按时间排序
            files = files.OrderBy(f => File.GetLastWriteTimeUtc(f)).ToList();

            // 限制初始批次的图片数量，避免一次性处理过多
            int maxBatchSize = 20; // 每次最多处理20张图片
            if (files.Count > maxBatchSize)
            {
                Log.Information("检测到{TotalFiles}张图片，限制本批次处理{MaxBatchSize}张", files.Count, maxBatchSize);
                files = files.Take(maxBatchSize).ToList();
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 检查文件是否存在，避免竞态条件
                if (!File.Exists(file))
                {
                    Log.Warning("文件已不存在，跳过处理: {FilePath}", file);
                    continue;
                }
                
                var lastWriteUtc = File.GetLastWriteTimeUtc(file);
                
                // 再次检查文件是否存在
                if (!File.Exists(file))
                {
                    Log.Warning("文件已不存在，跳过处理: {FilePath}", file);
                    continue;
                }
                
                long length;
                try
                {
                    length = new FileInfo(file).Length;
                }
                catch (FileNotFoundException)
                {
                    Log.Warning("文件已不存在，跳过处理: {FilePath}", file);
                    continue;
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "获取文件信息失败，跳过处理: {FilePath}", file);
                    continue;
                }
                
                if (_processed.TryGetValue(file, out var meta) &&
                    meta.LastWriteUtc == lastWriteUtc &&
                    meta.Length == length)
                {
                    continue;
                }

                // 检查文件大小，避免处理过大的文件
                if (length > 10 * 1024 * 1024) // 超过10MB的文件跳过
                {
                    Log.Warning("文件过大，跳过处理: {FilePath} ({FileSize}MB)", file, length / (1024 * 1024));
                    _processed[file] = (lastWriteUtc, length);
                    continue;
                }

                // 再次检查文件是否存在，避免竞态条件
                if (!File.Exists(file))
                {
                    Log.Warning("文件已不存在，跳过处理: {FilePath}", file);
                    _processed[file] = (lastWriteUtc, length);
                    continue;
                }

                var bytes = await TryReadAllBytesWithRetryAsync(file, cancellationToken);
                if (bytes == null || bytes.Length == 0)
                {
                    Log.Warning("文件读取失败，跳过处理: {FilePath}", file);
                    _processed[file] = (lastWriteUtc, length);
                    continue;
                }

                var fileMeta = ParseMetaFromFileName(file, options);
                results.Add(new FolderSnapshotResult
                {
                    ImageBytes = bytes,
                    CaptureTimeUtc = fileMeta.CaptureTimeUtc,
                    FilePath = file,
                    CameraIp = fileMeta.CameraIp,
                    CameraName = fileMeta.CameraName,
                    Location = fileMeta.Location,
                    Gender = fileMeta.Gender
                });

                _processed[file] = (lastWriteUtc, length);
            }

            CleanupMissingFiles(files);

            return results;
        }

        private void CleanupMissingFiles(List<string> files)
        {
            if (_processed.Count == 0) return;
            var existing = new HashSet<string>(files, StringComparer.OrdinalIgnoreCase);
            var toRemove = _processed.Keys.Where(k => !existing.Contains(k)).ToList();
            foreach (var key in toRemove)
                _processed.Remove(key);
        }

        private static async Task<byte[]?> TryReadAllBytesWithRetryAsync(string filePath, CancellationToken cancellationToken)
        {
            const int maxRetry = 3;
            for (int i = 0; i < maxRetry; i++)
            {
                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    Log.Warning("文件在读取过程中被删除: {FilePath}", filePath);
                    return null;
                }

                try
                {
                    return await File.ReadAllBytesAsync(filePath, cancellationToken);
                }
                catch (FileNotFoundException)
                {
                    Log.Warning("文件不存在: {FilePath}", filePath);
                    return null;
                }
                catch (IOException)
                {
                    // 检查是否是因为文件不存在导致的异常
                    if (!File.Exists(filePath))
                    {
                        Log.Warning("文件在读取过程中被删除: {FilePath}", filePath);
                        return null;
                    }
                    await Task.Delay(200, cancellationToken);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(200, cancellationToken);
                }
            }

            return null;
        }

        /// <summary>
        /// 解析文件名：IP_性别(1男0女)_时间，例如 192.168.1.108_1_20260202174649490_1_1
        /// </summary>
        private static (DateTime CaptureTimeUtc, string CameraIp, string CameraName, string Location, int Gender) ParseMetaFromFileName(
            string filePath,
            FtpFolderOptions options)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var parts = fileName.Split('_', StringSplitOptions.RemoveEmptyEntries);

            string cameraIp = "";
            string cameraName = options.DefaultCameraName;
            string location = options.DefaultLocation;
            DateTime captureTimeUtc = File.GetCreationTimeUtc(filePath);
            int gender = -1;

            // parts[0] = IP
            if (parts.Length > 0 && parts[0].Contains('.'))
            {
                cameraIp = parts[0];
                cameraName = parts[0];
            }

            // parts[1] = 性别：1=男，0=女
            if (parts.Length > 1 && (parts[1] == "0" || parts[1] == "1"))
            {
                gender = parts[1] == "1" ? 1 : 0;
            }

            // parts[2] = 时间，17位 yyyyMMddHHmmssfff 或 14位 yyyyMMddHHmmss
            if (parts.Length > 2 && parts[2].Length >= 14 && long.TryParse(parts[2], out _))
            {
                var timePart = parts[2].Length >= 17 ? parts[2].Substring(0, 17) : parts[2].Substring(0, 14);
                var format = timePart.Length == 17 ? "yyyyMMddHHmmssfff" : "yyyyMMddHHmmss";
                if (DateTime.TryParseExact(
                        timePart,
                        format,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeLocal,
                        out var localTime))
                {
                    captureTimeUtc = localTime.ToUniversalTime();
                }
            }

            return (captureTimeUtc, cameraIp, cameraName, location, gender);
        }
    }
}
