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
var builder = WebApplication.CreateBuilder(args);
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

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
