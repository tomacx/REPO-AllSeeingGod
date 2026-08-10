param(
    [string]$GameDir = "C:\Program Files (x86)\Steam\steamapps\common\REPO"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectRoot "src\AllSeeingGod.csproj"
$required = @(
    (Join-Path $GameDir "REPO.exe"),
    (Join-Path $GameDir "BepInEx\core\BepInEx.dll"),
    (Join-Path $GameDir "REPO_Data\Managed\UnityEngine.dll")
)

foreach ($file in $required) {
    if (-not (Test-Path $file)) {
        throw "缺少文件: $file。请用 -GameDir 指定正确的 R.E.P.O. 游戏目录，并先安装 BepInEx。"
    }
}

dotnet build $project -c Release -p:GameDir="$GameDir"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $projectRoot "src\bin\Release\netstandard2.1\AllSeeingGod.dll"
$pluginDir = Join-Path $GameDir "BepInEx\plugins\AllSeeingGod"
New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
Copy-Item $dll $pluginDir -Force
Write-Host "完成：$dll"
Write-Host "已安装到：$pluginDir"
