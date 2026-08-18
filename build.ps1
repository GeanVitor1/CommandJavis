param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "publish"

Write-Host "Compilando JarvisComando..." -ForegroundColor Cyan
dotnet publish "$root\JarvisComando.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item "$root\config.json" -Destination $out -Force -ErrorAction SilentlyContinue
if (-not (Test-Path "$out\config.json")) {
    Copy-Item "$root\config.json" -Destination $out -Force
}

Write-Host "Pronto: $out\JarvisComando.exe" -ForegroundColor Green

if ($Install) {
    $dest = Join-Path $env:LOCALAPPDATA "JarvisComando"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Copy-Item "$out\*" -Destination $dest -Recurse -Force

    $startup = [Environment]::GetFolderPath("Startup")
    $lnk = Join-Path $startup "JarvisComando.lnk"
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnk)
    $sc.TargetPath = Join-Path $dest "JarvisComando.exe"
    $sc.WorkingDirectory = $dest
    $sc.Save()
    Write-Host "Instalado e adicionado à inicialização do Windows." -ForegroundColor Green
}
