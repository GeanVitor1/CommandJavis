param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "publish"

function Stop-Vox {
    $procs = Get-Process Vox -ErrorAction SilentlyContinue
    if (-not $procs) { return }
    Write-Host "Encerrando Vox em execução..." -ForegroundColor Yellow
    foreach ($p in $procs) { try { $p.CloseMainWindow() | Out-Null } catch {} }
    Start-Sleep -Milliseconds 800
    $procs = Get-Process Vox -ErrorAction SilentlyContinue
    foreach ($p in $procs) { try { if (-not $p.HasExited) { $p.Kill() } } catch {} }
    Start-Sleep -Milliseconds 500
    # Aguarda mutex liberar
    for ($i = 0; $i -lt 20; $i++) {
        if (-not (Get-Process Vox -ErrorAction SilentlyContinue)) { break }
        Start-Sleep -Milliseconds 250
    }
}

function Prune-EspeakData {
    param([string]$EspeakDir)
    if (-not (Test-Path $EspeakDir)) { return }
    try {
        Get-ChildItem $EspeakDir -Filter "*_dict" -File |
            Where-Object { $_.Name -notin @("pt_dict", "en_dict") } |
            Remove-Item -Force
        $langDir = Join-Path $EspeakDir "lang"
        if (Test-Path $langDir) {
            Get-ChildItem $langDir -Directory |
                Where-Object { $_.Name -ne "roa" } |
                Remove-Item -Recurse -Force
            $roa = Join-Path $langDir "roa"
            if (Test-Path $roa) {
                Get-ChildItem $roa -File |
                    Where-Object { $_.Name -notin @("pt", "pt-BR") } |
                    Remove-Item -Force
            }
        }
        $voicesDir = Join-Path $EspeakDir "voices"
        if (Test-Path $voicesDir) { Remove-Item $voicesDir -Recurse -Force }
        Write-Host "espeak-ng-data podado para pt-BR." -ForegroundColor DarkGray
    } catch {
        Write-Warning "Falha ao podar espeak-ng-data: $($_.Exception.Message)"
    }
}

# Mata instância antiga antes de publicar (evita lock em publish/Vox.exe se estiver rodando de lá)
Stop-Vox

Write-Host "Compilando Vox..." -ForegroundColor Cyan
dotnet publish "$root\Vox.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item "$root\config.json" -Destination $out -Force -ErrorAction SilentlyContinue
if (-not (Test-Path "$out\config.json")) {
    Copy-Item "$root\config.json" -Destination $out -Force
}
Copy-Item "$root\voice" -Destination $out -Recurse -Force
Prune-EspeakData -EspeakDir (Join-Path $out "voice\espeak-ng-data")

Write-Host "Pronto: $out\Vox.exe" -ForegroundColor Green

if ($Install) {
    $dest = Join-Path $env:LOCALAPPDATA "Vox"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    Stop-Vox
    $installedConfig = Join-Path $dest "config.json"
    if (-not (Test-Path $installedConfig)) {
        Copy-Item "$out\config.json" -Destination $dest -Force
    }
    # Copia com retry — o exe pode ainda estar com lock por alguns ms
    $retries = 5
    for ($i = 1; $i -le $retries; $i++) {
        try {
            Copy-Item "$out\Vox.exe" -Destination $dest -Force -ErrorAction Stop
            Copy-Item "$out\Vox.pdb" -Destination $dest -Force -ErrorAction SilentlyContinue
            Copy-Item "$out\voice" -Destination $dest -Recurse -Force -ErrorAction Stop
            break
        } catch {
            if ($i -eq $retries) { throw }
            Write-Host "Aguardando liberação do arquivo... tentativa $i/$retries" -ForegroundColor DarkYellow
            Stop-Vox
            Start-Sleep -Milliseconds 600
        }
    }
    $startup = [Environment]::GetFolderPath("Startup")
    $lnk = Join-Path $startup "Vox.lnk"
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnk)
    $sc.TargetPath = Join-Path $dest "Vox.exe"
    $sc.WorkingDirectory = $dest
    $sc.Save()
    # Corrige Run key para apontar sempre para o instalado
    try {
        $runKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
        Set-ItemProperty -Path $runKey -Name "Vox" -Value ('"' + (Join-Path $dest "Vox.exe") + '"') -ErrorAction SilentlyContinue
    } catch {}
    Write-Host "Instalado em $dest e adicionado à inicialização." -ForegroundColor Green
    # Reinicia automaticamente para validar que a última versão subiu
    try { Start-Process (Join-Path $dest "Vox.exe") | Out-Null; Write-Host "Vox reiniciado." -ForegroundColor Green } catch {}
}
