# 批量调用 addFace 接口添加人脸摄像头
# 用法: .\batch-add-face-cameras.ps1 [-BaseUrl "http://localhost:5299"]

param(
    [string]$BaseUrl = "http://localhost:5299"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataPath = Join-Path $scriptDir "face-cameras-to-add.json"

if (-not (Test-Path $dataPath)) {
    Write-Error "数据文件不存在: $dataPath"
    exit 1
}

$list = Get-Content $dataPath -Raw -Encoding UTF8 | ConvertFrom-Json
$total = $list.Count
$ok = 0
$fail = 0

Write-Host "共 $total 条人脸摄像头，开始调用 $BaseUrl/api/camera/addFace ..." -ForegroundColor Cyan

foreach ($item in $list) {
    $body = @{ cameraIp = $item.cameraIp; description = $item.description } | ConvertTo-Json -Compress
    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/camera/addFace" -Method Post -Body $body -ContentType "application/json; charset=utf-8" -TimeoutSec 10
        if ($resp.code -eq 200) {
            $ok++
            Write-Host "  [OK] $($item.cameraIp) $($item.description)" -ForegroundColor Green
        } else {
            $fail++
            Write-Host "  [FAIL] $($item.cameraIp) code=$($resp.code) msg=$($resp.msg)" -ForegroundColor Yellow
        }
    } catch {
        $fail++
        Write-Host "  [ERR] $($item.cameraIp) $($_.Exception.Message)" -ForegroundColor Red
    }
}

Write-Host "`n完成: 成功 $ok, 失败 $fail / $total" -ForegroundColor Cyan
