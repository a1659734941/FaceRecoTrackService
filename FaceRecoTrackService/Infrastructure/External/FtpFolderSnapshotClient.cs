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

            foreach (var file in files.OrderBy(f => File.GetLastWriteTimeUtc(f)))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lastWriteUtc = File.GetLastWriteTimeUtc(file);
                var length = new FileInfo(file).Length;
                if (_processed.TryGetValue(file, out var meta) &&
                    meta.LastWriteUtc == lastWriteUtc &&
                    meta.Length == length)
                {
                    continue;
                }

                var bytes = await TryReadAllBytesWithRetryAsync(file, cancellationToken);
                if (bytes == null || bytes.Length == 0) continue;

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
                try
                {
                    return await File.ReadAllBytesAsync(filePath, cancellationToken);
                }
                catch (IOException)
                {
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
