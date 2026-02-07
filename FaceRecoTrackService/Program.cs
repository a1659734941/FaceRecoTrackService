using FaceRecoTrackService.Core.Algorithms;
using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Services;
using FaceRecoTrackService.Utils.QdrantUtil;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client.Grpc;
using Serilog;
using Serilog.Events;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.IO;

// 设置当前工作目录为可执行文件所在的目录
var exeDir = AppContext.BaseDirectory;
Directory.SetCurrentDirectory(exeDir);

var builder = WebApplication.CreateBuilder(args);
// 配置为Windows服务
builder.Host.UseWindowsService(options =>
{
    options.ServiceName = "FaceTrackService";
});

// 仅在非服务模式下设置控制台编码
if (Environment.UserInteractive)
{
    Console.InputEncoding = Encoding.UTF8;
    Console.OutputEncoding = Encoding.UTF8;
}

var logRoot = Path.Combine("logs", DateTime.Now.ToString("yyyy"), DateTime.Now.ToString("MM"));
Directory.CreateDirectory(logRoot);
var logPath = Path.Combine(logRoot, "FaceTrackService-.log");
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.File(
        logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 31,
        shared: true);

if (builder.Environment.IsDevelopment())
{
    loggerConfig.MinimumLevel.Debug();
    loggerConfig.WriteTo.Console();
}
else if (Environment.UserInteractive)
{
    // 在非开发模式但交互模式下也输出到控制台
    loggerConfig.WriteTo.Console();
}

Log.Logger = loggerConfig.CreateLogger();
builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS：允许前端跨域访问
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins != null && allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        }
    });
});

builder.Services.AddSwaggerGen(options =>
{
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFilename), includeControllerXmlComments: true);
});

builder.Services.Configure<FaceRecognitionOptions>(builder.Configuration.GetSection("FaceRecognition"));
builder.Services.Configure<PipelineOptions>(builder.Configuration.GetSection("Pipeline"));
builder.Services.Configure<FtpFolderOptions>(builder.Configuration.GetSection("FtpFolder"));
builder.Services.Configure<PayloadMappingOptions>(builder.Configuration.GetSection("PayloadMapping"));
builder.Services.Configure<CameraRoomConfig>(builder.Configuration.GetSection("CameraRoomConfig"));
builder.Services.Configure<QdrantConfig>(builder.Configuration.GetSection("Qdrant"));

var pgConnectionString = builder.Configuration.GetConnectionString("Postgres") ?? "";
builder.Services.AddSingleton(new PgSchemaInitializer(pgConnectionString));
builder.Services.AddSingleton(new PgFaceRepository(pgConnectionString));
builder.Services.AddSingleton(new PgTrackRepository(pgConnectionString));
builder.Services.AddSingleton(new PgCameraMappingRepository(pgConnectionString));
builder.Services.AddSingleton(new PgFaceCameraRepository(pgConnectionString));
builder.Services.AddSingleton(new PgRecordCameraRepository(pgConnectionString));

builder.Services.AddSingleton<QdrantVectorManager>(sp =>
{
    var cfg = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantConfig>>().Value;
    return new QdrantVectorManager(cfg);
});

builder.Services.AddSingleton(sp =>
{
    return sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<CameraRoomConfig>>().Value;
});

// 注册配置对象的直接实例，供需要直接类型（而非IOptions<>）的服务使用
builder.Services.AddSingleton<FaceRecognitionOptions>(sp =>
{
    return sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<FaceRecognitionOptions>>().Value;
});

builder.Services.AddSingleton<QdrantConfig>(sp =>
{
    return sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantConfig>>().Value;
});

builder.Services.AddScoped<FaceRegistrationService>();
builder.Services.AddScoped<FaceDeletionService>();
builder.Services.AddScoped<FaceQueryService>();
builder.Services.AddScoped<FaceVerificationService>();
builder.Services.AddScoped<TrackQueryService>();
builder.Services.AddScoped<TrackRecordService>();
builder.Services.AddScoped<CameraService>();

builder.Services.AddHostedService<FtpRecognitionWorker>();

var app = builder.Build();

// 检测硬件配置
DetectHardwareConfig(app.Logger);

// 预加载模型
await PreloadModels(app.Services, app.Logger, app.Lifetime.ApplicationStopping);

var qdrantConfig = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantConfig>>().Value;
Process? qdrantProcess = null;
try
{
    qdrantProcess = await QdrantEmbeddedBootstrapper.EnsureStartedAsync(
        qdrantConfig,
        app.Logger,
        app.Lifetime.ApplicationStopping);
}
catch (Exception ex)
{
    app.Logger.LogError(ex, "内置Qdrant启动失败");
    throw;
}

if (qdrantProcess != null)
{
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        try
        {
            if (!qdrantProcess.HasExited)
            {
                qdrantProcess.Kill(true);
            }
        }
        catch
        {
            // 忽略停止异常
        }
    });
}

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<PgSchemaInitializer>();
    await initializer.EnsureSchemaAsync(app.Lifetime.ApplicationStopping);

    var qdrant = scope.ServiceProvider.GetRequiredService<QdrantVectorManager>();
    var faceOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FaceRecognitionOptions>>().Value;
    await qdrant.EnsureCollectionAsync(
        qdrantConfig.CollectionName,
        faceOptions.VectorSize,
        qdrantConfig.RecreateOnVectorSizeMismatch,
        Distance.Cosine,
        cancellationToken: app.Lifetime.ApplicationStopping);
}
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

try
{
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}

// 检测硬件配置
static void DetectHardwareConfig(Microsoft.Extensions.Logging.ILogger logger)
{
    logger.LogInformation("=== 硬件配置信息 ===");
    
    // 检测CPU信息
    try
    {
        logger.LogInformation($"CPU核心数: {Environment.ProcessorCount}");
        // 尝试获取进程信息
        using var process = Process.GetCurrentProcess();
        logger.LogInformation($"进程ID: {process.Id}");
    }
    catch (Exception ex)
    {
        logger.LogWarning($"获取CPU信息失败: {ex.Message}");
    }
    
    // 检测RAM信息
    try
    {
        // 使用Runtime获取内存信息
        using var process = Process.GetCurrentProcess();
        var memoryMB = process.WorkingSet64 / (1024 * 1024);
        logger.LogInformation($"进程内存使用: {memoryMB} MB");
        logger.LogInformation($".NET运行时版本: {Environment.Version}");
    }
    catch (Exception ex)
    {
        logger.LogWarning($"获取RAM信息失败: {ex.Message}");
    }
    
    // 检测GPU信息（简化版）
    try
    {
        // 检查是否有GPU加速可用
        var hasGpu = false;
        try
        {
            // 尝试检查ONNX Runtime是否支持CUDA
            var sessionOptions = new Microsoft.ML.OnnxRuntime.SessionOptions();
            try
            {
                sessionOptions.AppendExecutionProvider_CUDA();
                hasGpu = true;
            }
            catch
            {
                // CUDA不可用
            }
        }
        catch
        {
            // 忽略错误
        }
        
        if (hasGpu)
        {
            logger.LogInformation("GPU: 检测到GPU加速支持");
        }
        else
        {
            logger.LogInformation("GPU: 未检测到GPU加速支持");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning($"获取GPU信息失败: {ex.Message}");
        logger.LogInformation("GPU: 检测失败");
    }
    
    logger.LogInformation("==================");
}

// 预加载模型
static async Task PreloadModels(IServiceProvider services, Microsoft.Extensions.Logging.ILogger logger, CancellationToken cancellationToken)
{
    logger.LogInformation("开始预加载模型...");
    
    using var scope = services.CreateScope();
    var faceOptions = scope.ServiceProvider.GetRequiredService<FaceRecognitionOptions>();
    
    try
    {
        // 预加载人脸检测器模型（YOLOv8s-face）
        var detector = FaceDetector.GetInstance(faceOptions);
        logger.LogInformation($"加载了人脸检测模型: {Path.GetFileName(faceOptions.YoloModelPath)}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"加载人脸检测模型失败: {faceOptions.YoloModelPath}");
    }
    
    try
    {
        // 预加载人脸特征提取模型（ArcFace）
        var featureService = FaceFeatureService.GetInstance(faceOptions);
        logger.LogInformation($"加载了人脸特征提取模型: {Path.GetFileName(faceOptions.FaceNetModelPath)}");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"加载人脸特征提取模型失败: {faceOptions.FaceNetModelPath}");
    }
    
    logger.LogInformation("模型预加载完成");
}
