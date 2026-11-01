# 构建云财税务助手安装包：dotnet publish 自包含产物 -> Inno Setup 打包 -> 输出到 ui/public/downloads。
# 依赖：.NET SDK 10、Inno Setup 6（ISCC.exe 在 PATH 或默认安装目录）。
# 用法（仓库根目录）：powershell -ExecutionPolicy Bypass -File scripts/build-tax-assistant.ps1
[CmdletBinding()]
param(
    [string]$Version,
    [string]$OutputDir
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src/TaxAssistant/KuaijiTool.csproj"
$iss = Join-Path $repoRoot "src/TaxAssistant/installer/KuaijiTaxAssistant.iss"
$stage = Join-Path $repoRoot "src/TaxAssistant/obj/installer-stage"
if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "ui/public/downloads"
}

if (-not $Version) {
    $assemblyInfo = Get-Content (Join-Path $repoRoot "src/TaxAssistant/Properties/AssemblyInfo.cs") -Raw
    if ($assemblyInfo -match 'AssemblyVersion\("([^"]+)"\)') {
        $Version = $Matches[1]
    }
    else {
        throw "无法从 AssemblyInfo.cs 读取版本号，请用 -Version 指定"
    }
}

$iscc = $null
$command = Get-Command iscc.exe -ErrorAction SilentlyContinue
if ($command) {
    $iscc = $command.Source
}
else {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 6/ISCC.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs/Inno Setup 7/ISCC.exe"),
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 7\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            $iscc = $candidate
            break
        }
    }
}
if (-not $iscc) {
    throw "未找到 Inno Setup 的 ISCC.exe，请先安装 Inno Setup 6（winget install JRSoftware.InnoSetup）"
}

if (Get-Process KuaijiTool -ErrorAction SilentlyContinue) {
    throw "云财税务助手正在运行，请先退出后重新构建"
}

if (Test-Path $stage) {
    Remove-Item $stage -Recurse -Force
}
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "发布助手 $Version（win-x86，自包含）..."
dotnet publish $project -c Release -r win-x86 --self-contained true -o $stage --nologo -p:DebugType=None
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish 失败"
}

New-Item -ItemType Directory -Force $OutputDir | Out-Null

Write-Host "生成安装包..."
& $iscc "/DAppVersion=$Version" "/DSourceDir=$stage" "/DOutputDir=$OutputDir" $iss
if ($LASTEXITCODE -ne 0) {
    throw "Inno Setup 编译失败"
}

$setup = Join-Path $OutputDir "KuaijiTaxAssistantSetup.exe"
$size = [math]::Round((Get-Item $setup).Length / 1MB, 1)
Write-Host "已生成 $setup（$size MB）"
