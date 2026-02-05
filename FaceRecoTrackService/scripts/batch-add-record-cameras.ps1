# 批量调用 addRecord 接口添加录像摄像头（格式 ip-locationName）
# 用法: .\batch-add-record-cameras.ps1 [-BaseUrl "http://127.0.0.1:12355"]

param(
    [string]$BaseUrl = "http://127.0.0.1:12355"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$dataPath = Join-Path $scriptDir "record-cameras-to-add.json"

if (-not (Test-Path $dataPath)) {
    Write-Error "数据文件不存在: $dataPath"
    exit 1
}

$list = Get-Content $dataPath -Raw -Encoding UTF8 | ConvertFrom-Json
$total = $list.Count
$ok = 0
$fail = 0

Write-Host "共 $total 条录像摄像头，开始调用 $BaseUrl/api/camera/addRecord ..." -ForegroundColor Cyan

foreach ($item in $list) {
    $body = @{ cameraIp = $item.cameraIp; locationName = $item.locationName } | ConvertTo-Json -Compress
    try {
        $resp = Invoke-RestMethod -Uri "$BaseUrl/api/camera/addRecord" -Method Post -Body $body -ContentType "application/json; charset=utf-8" -TimeoutSec 10
        if ($resp.code -eq 200) {
            $ok++
            Write-Host "  [OK] $($item.cameraIp) $($item.locationName)" -ForegroundColor Green
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
