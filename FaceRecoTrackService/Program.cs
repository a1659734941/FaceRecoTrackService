using FaceRecoTrackService.Core.Options;
using FaceRecoTrackService.Infrastructure.Repositories;
using FaceRecoTrackService.Services;
using FaceRecoTrackService.Utils.QdrantUtil;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client.Grpc;
using System.Reflection;
using System.Text;
var builder = WebApplication.CreateBuilder(args);
Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

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

builder.Services.AddHostedService<FtpRecognitionWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<PgSchemaInitializer>();
    await initializer.EnsureSchemaAsync(app.Lifetime.ApplicationStopping);

    var qdrant = scope.ServiceProvider.GetRequiredService<QdrantVectorManager>();
    var faceOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<FaceRecognitionOptions>>().Value;
    var qdrantOptions = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<QdrantConfig>>().Value;
    await qdrant.EnsureCollectionAsync(
        qdrantOptions.CollectionName,
        faceOptions.VectorSize,
        qdrantOptions.RecreateOnVectorSizeMismatch,
        Distance.Cosine,
        cancellationToken: app.Lifetime.ApplicationStopping);
}
app.UseSwagger();
app.UseSwaggerUI();


app.UseHttpsRedirection();
app.MapControllers();

app.Run();
