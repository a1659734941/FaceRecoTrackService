# FaceRecoTrackService - 人脸识别与追踪服务

## 📋 产品介绍

FaceRecoTrackService 是一个基于 .NET 8.0 开发的高性能人脸识别与追踪服务系统。该系统集成了深度学习模型（YOLOv8 人脸检测 + FaceNet 特征提取）、向量数据库（Qdrant）和关系型数据库（PostgreSQL），实现了完整的人脸注册、识别和轨迹追踪功能。

### 核心特性

- **高精度人脸检测**：基于 YOLOv8s-face 模型，支持实时人脸检测
- **特征向量提取**：使用 FaceNet Inception ResNetV1 模型提取 128 维特征向量
- **向量相似度检索**：集成 Qdrant 向量数据库，支持高效的相似度搜索
- **实时监控识别**：通过 FTP 文件夹监控，自动处理新的人脸快照
- **轨迹追踪记录**：记录人员在不同摄像头间的移动轨迹和时间信息
- **清晰度筛选**：自动过滤模糊人脸，确保识别质量
- **RESTful API**：提供完整的 HTTP API 接口，支持人脸注册、查询、删除和轨迹查询

### 技术栈

- **框架**：.NET 8.0 (ASP.NET Core)
- **数据库**：PostgreSQL 10.0+
- **向量数据库**：Qdrant 1.16+
- **深度学习**：ONNX Runtime 1.23+
- **图像处理**：EmguCV 4.12+, SkiaSharp 3.119+
- **API 文档**：Swagger/OpenAPI

---

## 🚀 快速开始

### 系统要求

- Windows 10/11 或 Windows Server 2016+
- .NET 8.0 Runtime（单文件发布版本无需安装）
- PostgreSQL 10.0+ 数据库
- Qdrant 向量数据库服务

### 环境准备

1. **安装 PostgreSQL**
   - 下载并安装 PostgreSQL：https://www.postgresql.org/download/
   - 创建数据库（或使用默认的 postgres 数据库）

2. **安装 Qdrant**
   - 使用 Docker：`docker run -p 6333:6333 qdrant/qdrant`
   - 或下载 Windows 版本：https://qdrant.tech/documentation/guides/installation/

3. **准备模型文件**
   - 确保 `res/model/` 目录包含以下模型文件：
     - `yolov8s-face.onnx` - 人脸检测模型
     - `facenet_inception_resnetv1.onnx` - 人脸特征提取模型

### 配置说明

编辑 `appsettings.json` 文件，配置以下关键参数：

```json
{
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Port=5432;Username=postgres;Password=your_password;Database=postgres"
  },
  "Qdrant": {
    "Host": "localhost",
    "Port": 6334,
    "CollectionName": "face_collection",
    "UseHttps": false,
    "ApiKey": "",
    "RecreateOnVectorSizeMismatch": true
  },
  "FaceRecognition": {
    "YoloModelPath": "res/model/yolov8s-face.onnx",
    "FaceNetModelPath": "res/model/facenet_inception_resnetv1.onnx",
    "DetectionConfidence": 0.45,
    "IouThreshold": 0.45,
    "FaceExpandRatio": 20,
    "BaseSharpnessThreshold": 15.0,
    "SizeThresholdCoefficient": 0.0002,
    "VectorSize": 128,
    "EnableDebugSaveFaces": false,
    "DebugSaveDir": "snapshots/registrations"
  },
  "Pipeline": {
    "PollIntervalMs": 2000,
    "MinFaceCount": 1,
    "TopK": 5,
    "SimilarityThreshold": 0.87,
    "FallbackSimilarityThreshold": 0.78,
    "SnapshotSaveDir": "snapshots",
    "DeleteProcessedSnapshots": true
  },
  "FtpFolder": {
    "Path": "res/ftp",
    "IncludeSubdirectories": true,
    "FilePatterns": [ "*.jpg", "*.jpeg", "*.png" ],
    "DefaultCameraName": "unknown",
    "DefaultLocation": "unknown"
  }
}
```

### 打包部署

#### 方式一：使用打包脚本（推荐）

1. 运行打包脚本：
   ```batch
   build.bat
   ```

2. 打包完成后，在 `dist/publish/` 目录下找到：
   - `FaceRecoTrackService.exe` - 单文件可执行程序
   - `res/` - 模型文件目录
   - `appsettings.json` - 配置文件

3. 将整个 `publish` 目录复制到目标服务器

#### 方式二：手动打包

```batch
dotnet publish FaceRecoTrackService/FaceRecoTrackService.csproj ^
    --configuration Release ^
    --runtime win-x64 ^
    --self-contained true ^
    --output dist/publish ^
    /p:PublishSingleFile=true ^
    /p:IncludeNativeLibrariesForSelfExtract=true ^
    /p:EnableCompressionInSingleFile=true
```

### 运行服务

1. **直接运行**
   ```batch
   cd dist/publish
   FaceRecoTrackService.exe
   ```

2. **作为 Windows 服务运行**（需要额外配置）
   - 使用 NSSM 或 Windows Service Wrapper
   - 或使用 .NET Worker Service 模板

3. **访问 API 文档**
   - 开发环境：http://localhost:5000/swagger
   - 生产环境：根据配置的端口访问

---

## 📚 功能说明

### 1. 人脸注册

通过 API 接口注册新的人脸信息，系统会：
- 检测图片中的人脸
- 筛选清晰的人脸（基于拉普拉斯方差）
- 提取人脸特征向量（128 维）
- 将信息存储到 PostgreSQL 和 Qdrant

**API 端点**：`POST /api/face/register`

**请求示例**：
```json
{
  "base64Image": "data:image/jpeg;base64,/9j/4AAQSkZJRg...",
  "userName": "张三",
  "ip": "192.168.1.100",
  "description": "测试用户",
  "isTest": false
}
```

**响应示例**：
```json
{
  "success": true,
  "message": "注册成功",
  "data": {
    "id": "550e8400-e29b-41d4-a716-446655440000"
  }
}
```

### 2. 实时识别监控

系统通过后台服务 `FtpRecognitionWorker` 持续监控指定文件夹：
- 轮询间隔：默认 2000ms（可配置）
- 自动检测新的人脸快照文件
- 提取人脸特征并与已注册人脸进行相似度匹配
- 记录识别结果到轨迹表

**工作流程**：
1. 监控 `FtpFolder.Path` 目录下的图片文件
2. 检测图片中的人脸
3. 提取特征向量并在 Qdrant 中搜索相似人脸
4. 如果相似度超过阈值，记录轨迹信息
5. 处理完成后可选择删除已处理的快照文件

### 3. 轨迹查询

根据人员 ID 查询其在不同摄像头间的移动轨迹。

**API 端点**：`GET /api/track/{id}?pageNum=1&pageSize=20`

**响应示例**：
```json
{
  "success": true,
  "message": "查询guid轨迹成功",
  "data": {
    "list": [
      {
        "id": "550e8400-e29b-41d4-a716-446655440000",
        "抓拍时间": "2026-01-24 10:30:15",
        "抓拍地点": "大厅",
        "抓拍摄像头": "192.168.1.101",
        "录像摄像头": "192.168.1.101",
        "录像开始时间": "2026-01-24 10:30:15",
        "录像结束时间": "2026-01-24 10:35:20"
      }
    ],
    "pagesize": 20,
    "pagenum": 1,
    "total": 1
  }
}
```

### 4. 人脸查询与管理

- **查询人脸数量**：`GET /api/face/count` - 获取 PostgreSQL 中注册的人脸数量
- **查询 Qdrant 数量**：`GET /api/face/qdrant/count` - 获取 Qdrant 向量库中的向量数量
- **获取人脸信息**：`GET /api/face/getfaceinfo/{id}` - 根据 ID 获取人脸详细信息
- **删除人脸**：`DELETE /api/face/deletefaceinfo/{id}` - 删除指定的人脸信息（同时删除 PostgreSQL 和 Qdrant 中的数据）

---

## 🔧 API 接口文档

### 人脸管理接口

#### 注册人脸
- **URL**：`POST /api/face/register`
- **请求体**：
  ```json
  {
    "base64Image": "string (Base64编码的图片)",
    "userName": "string (用户名)",
    "ip": "string (可选，IP地址)",
    "description": "string (可选，描述信息)",
    "isTest": "boolean (可选，是否测试数据)"
  }
  ```
- **响应**：返回注册成功的人脸 ID（GUID）

#### 查询人脸数量
- **URL**：`GET /api/face/count`
- **响应**：返回 PostgreSQL 中的人脸记录数量

#### 查询 Qdrant 向量数量
- **URL**：`GET /api/face/qdrant/count`
- **响应**：返回 Qdrant 集合中的向量数量

#### 获取人脸信息
- **URL**：`GET /api/face/getfaceinfo/{id}`
- **参数**：`id` - 人脸唯一标识（GUID）
- **响应**：返回人脸详细信息（不包含 Base64 图片）

#### 删除人脸
- **URL**：`DELETE /api/face/deletefaceinfo/{id}`
- **参数**：`id` - 人脸唯一标识（GUID）
- **响应**：返回删除结果

### 轨迹查询接口

#### 查询人员轨迹
- **URL**：`GET /api/track/{id}?pageNum=1&pageSize=20`
- **参数**：
  - `id` - 人员唯一标识（GUID）
  - `pageNum` - 页码（默认：1）
  - `pageSize` - 每页数量（默认：20）
- **响应**：返回分页的轨迹记录列表

---

## 🏗️ 架构设计

### 项目结构

```
FaceRecoTrackService/
├── API/
│   └── Controllers/          # API 控制器
│       ├── FaceController.cs  # 人脸管理接口
│       └── TrackController.cs # 轨迹查询接口
├── Core/
│   ├── Algorithms/           # 核心算法
│   │   ├── FaceDetector.cs   # 人脸检测器
│   │   ├── FaceFeatureService.cs # 特征提取服务
│   │   ├── ImageUtils.cs     # 图像工具类
│   │   └── SharpnessEvaluator.cs # 清晰度评估器
│   ├── Dtos/                 # 数据传输对象
│   ├── Models/               # 数据模型
│   └── Options/              # 配置选项类
├── Infrastructure/
│   ├── Database/             # 数据库上下文
│   ├── External/             # 外部服务客户端
│   └── Repositories/         # 数据仓储
├── Services/                 # 业务服务
│   ├── FaceRegistrationService.cs # 人脸注册服务
│   ├── FaceDeletionService.cs # 人脸删除服务
│   ├── FaceQueryService.cs   # 人脸查询服务
│   ├── FtpRecognitionWorker.cs # FTP 监控后台服务
│   ├── TrackQueryService.cs  # 轨迹查询服务
│   └── TrackRecordService.cs # 轨迹记录服务
├── Utils/                    # 工具类
│   └── QdrantUtil/           # Qdrant 工具
├── res/                      # 资源文件
│   └── model/                # AI 模型文件
│       ├── yolov8s-face.onnx
│       └── facenet_inception_resnetv1.onnx
├── Program.cs                # 程序入口
└── appsettings.json          # 配置文件
```

### 核心组件说明

#### 1. FaceDetector（人脸检测器）
- **功能**：使用 YOLOv8s-face ONNX 模型检测图片中的人脸
- **主要方法**：
  - `DetectFaces(SKImage image)` - 检测人脸并返回检测结果列表
  - `CropAndFilterSharpFaces()` - 裁剪人脸并筛选清晰的人脸

#### 2. FaceFeatureService（特征提取服务）
- **功能**：使用 FaceNet 模型提取人脸特征向量
- **主要方法**：
  - `ExtractFeaturesFromStream(Stream imageStream)` - 从流中提取特征向量
  - `ExtractFeatures(string imagePath)` - 从文件路径提取特征向量
  - `CalculateSimilarity(float[] feat1, float[] feat2)` - 计算两个向量的余弦相似度

#### 3. SharpnessEvaluator（清晰度评估器）
- **功能**：基于拉普拉斯方差评估人脸清晰度
- **主要方法**：
  - `IsSharp()` - 判断人脸是否清晰
  - `GetDynamicThreshold()` - 根据人脸尺寸计算动态阈值

#### 4. FtpRecognitionWorker（后台监控服务）
- **功能**：持续监控 FTP 文件夹，自动处理新的人脸快照
- **工作流程**：
  1. 定期扫描配置的文件夹
  2. 检测新文件并读取图片
  3. 使用 FaceDetector 检测人脸
  4. 提取特征并在 Qdrant 中搜索匹配
  5. 记录轨迹信息到数据库

#### 5. QdrantVectorManager（向量数据库管理器）
- **功能**：管理 Qdrant 向量数据库的连接和操作
- **主要操作**：
  - 创建/确保集合存在
  - 插入/更新向量点
  - 相似度搜索

---

## 🔄 移植说明

### 从其他环境移植

#### 1. 数据库迁移

**PostgreSQL 表结构**：
系统会在启动时自动创建所需的表结构。主要表包括：
- `face_persons` - 人脸信息表
- `track_records` - 轨迹记录表
- `camera_mappings` - 摄像头映射表

**迁移步骤**：
1. 备份原数据库数据
2. 在新环境中创建 PostgreSQL 数据库
3. 启动服务，系统会自动创建表结构
4. 如需迁移数据，使用 PostgreSQL 的 `pg_dump` 和 `pg_restore` 工具

#### 2. Qdrant 迁移

**迁移步骤**：
1. 导出原 Qdrant 集合数据：
   ```bash
   curl -X POST "http://old-qdrant:6333/collections/{collection_name}/points/scroll" \
     -H "Content-Type: application/json" \
     -d '{"limit": 10000}'
   ```
2. 在新环境中创建同名集合
3. 导入数据到新 Qdrant 实例

#### 3. 模型文件迁移

确保以下模型文件存在于 `res/model/` 目录：
- `yolov8s-face.onnx` - 人脸检测模型（约 20-30MB）
- `facenet_inception_resnetv1.onnx` - 特征提取模型（约 90-100MB）

#### 4. 配置文件调整

根据新环境修改 `appsettings.json`：
- 数据库连接字符串
- Qdrant 服务器地址和端口
- FTP 监控路径
- 其他业务配置参数

#### 5. 依赖环境检查

**必需组件**：
- .NET 8.0 Runtime（单文件发布无需）
- PostgreSQL 客户端库（已包含在发布包中）
- ONNX Runtime（已包含在发布包中）
- EmguCV 运行时（已包含在发布包中）

**网络要求**：
- 确保服务可以访问 PostgreSQL 数据库（默认端口 5432）
- 确保服务可以访问 Qdrant 服务（默认端口 6333/6334）
- 如果使用 HTTPS，确保证书配置正确

### 跨平台移植

当前版本针对 Windows x64 平台编译。如需移植到 Linux：

1. **修改项目文件**：
   ```xml
   <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
   ```

2. **修改打包脚本**：
   - 将运行时标识符改为 `linux-x64`
   - 注意文件路径分隔符（Linux 使用 `/`）

3. **依赖库调整**：
   - EmguCV 需要使用 Linux 版本
   - 确保所有原生库都有 Linux 版本

---

## ⚙️ 配置参数详解

### FaceRecognition 配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| YoloModelPath | string | "res/model/yolov8s-face.onnx" | YOLO 人脸检测模型路径 |
| FaceNetModelPath | string | "res/model/facenet_inception_resnetv1.onnx" | FaceNet 特征提取模型路径 |
| DetectionConfidence | float | 0.45 | 人脸检测置信度阈值 |
| IouThreshold | float | 0.45 | NMS 交并比阈值 |
| FaceExpandRatio | int | 20 | 人脸裁剪扩展像素数 |
| BaseSharpnessThreshold | double | 15.0 | 基础清晰度阈值 |
| SizeThresholdCoefficient | double | 0.0002 | 尺寸阈值系数 |
| VectorSize | int | 128 | 特征向量维度 |
| EnableDebugSaveFaces | bool | false | 是否保存调试人脸图片 |
| DebugSaveDir | string | "snapshots/registrations" | 调试图片保存目录 |

### Pipeline 配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| PollIntervalMs | int | 2000 | FTP 文件夹轮询间隔（毫秒） |
| MinFaceCount | int | 1 | 最少人脸数量要求 |
| TopK | int | 5 | 相似度搜索返回的 Top K 结果 |
| SimilarityThreshold | float | 0.87 | 主要相似度阈值 |
| FallbackSimilarityThreshold | float | 0.78 | 备用相似度阈值 |
| SnapshotSaveDir | string | "snapshots" | 快照保存目录 |
| DeleteProcessedSnapshots | bool | true | 是否删除已处理的快照文件 |

### FtpFolder 配置

| 参数 | 类型 | 默认值 | 说明 |
|------|------|--------|------|
| Path | string | "res/ftp" | 监控的文件夹路径 |
| IncludeSubdirectories | bool | true | 是否包含子目录 |
| FilePatterns | string[] | ["*.jpg", "*.jpeg", "*.png"] | 文件匹配模式 |
| DefaultCameraName | string | "unknown" | 默认摄像头名称 |
| DefaultLocation | string | "unknown" | 默认位置名称 |

---

## 🐛 故障排查

### 常见问题

#### 1. 模型文件加载失败
**症状**：启动时提示模型文件不存在或加载失败

**解决方案**：
- 检查 `res/model/` 目录是否存在
- 确认模型文件路径配置正确
- 检查文件权限

#### 2. 数据库连接失败
**症状**：无法连接到 PostgreSQL 数据库

**解决方案**：
- 检查连接字符串配置
- 确认 PostgreSQL 服务正在运行
- 检查网络连接和防火墙设置
- 验证用户名和密码

#### 3. Qdrant 连接失败
**症状**：无法连接到 Qdrant 服务

**解决方案**：
- 检查 Qdrant 服务是否运行
- 验证主机和端口配置
- 检查 API Key（如果启用）

#### 4. 人脸检测失败
**症状**：注册或识别时无法检测到人脸

**解决方案**：
- 检查图片质量（清晰度、光照）
- 调整 `DetectionConfidence` 参数
- 确认模型文件完整且未损坏

#### 5. 相似度匹配不准确
**症状**：识别结果不准确或误识别

**解决方案**：
- 调整 `SimilarityThreshold` 和 `FallbackSimilarityThreshold`
- 检查特征向量维度是否匹配
- 确保注册时使用清晰的人脸图片

### 日志查看

服务运行时会输出日志到控制台。建议在生产环境中配置日志文件输出：

在 `appsettings.json` 中配置：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    },
    "File": {
      "Path": "logs/app.log",
      "Append": true
    }
  }
}
```

---

## 📝 开发指南

### 添加新功能

1. **添加新的 API 接口**：
   - 在 `API/Controllers/` 中创建新的控制器
   - 在 `Services/` 中实现业务逻辑
   - 在 `Program.cs` 中注册服务

2. **扩展数据模型**：
   - 在 `Core/Models/` 中定义新模型
   - 在 `Infrastructure/Repositories/` 中实现数据访问
   - 更新数据库初始化脚本

3. **集成新的 AI 模型**：
   - 将模型文件放入 `res/model/` 目录
   - 创建对应的服务类加载和使用模型
   - 在配置文件中添加模型路径配置

### 性能优化建议

1. **模型加载优化**：
   - 使用单例模式管理模型实例
   - 避免重复加载模型

2. **数据库优化**：
   - 为常用查询字段添加索引
   - 使用连接池管理数据库连接

3. **向量搜索优化**：
   - 合理设置 `TopK` 参数
   - 使用适当的相似度阈值减少搜索范围

---

## 📄 许可证

本项目为内部项目，版权归公司所有。

---

## 👥 联系方式

如有问题或建议，请联系开发团队。

---

## 🔄 更新日志

### v1.0.0 (2026-01-24)
- 初始版本发布
- 实现人脸注册、识别和轨迹追踪功能
- 支持单文件打包部署
- 集成 Swagger API 文档

---

**最后更新**：2026-01-24
