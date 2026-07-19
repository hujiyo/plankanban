# 生成 Plan Kanban 单文件 exe
# 需求：.NET 8 SDK（winget install Microsoft.DotNet.SDK.8）

$ErrorActionPreference = 'Stop'

Write-Host "==> 发布 Plan Kanban 单文件 exe ..." -ForegroundColor Cyan
dotnet publish "$PSScriptRoot\PlanKanban.csproj" `
  -c Release `
  -o "$PSScriptRoot\publish" `
  --nologo

$out = Join-Path $PSScriptRoot 'publish\PlanKanban.exe'
if (Test-Path $out) {
    $size = [math]::Round((Get-Item $out).Length / 1MB, 2)
    Write-Host ""
    Write-Host "==> 构建成功：$out" -ForegroundColor Green
    Write-Host "==> 文件体积：$size MB" -ForegroundColor Green
} else {
    Write-Host "!! 构建失败，未生成 exe" -ForegroundColor Red
    exit 1
}