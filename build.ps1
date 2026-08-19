param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$out = Join-Path $root "publish"

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
        if (Test-Path $voicesDir) {
            Remove-Item $voicesDir -Recurse -Force
        }

        Write-Host "espeak-ng-data podado para pt-BR." -ForegroundColor DarkGray
    }
    catch {
        Write-Warning "Falha ao podar espeak-ng-data: $($_.Exception.Message)"
    }
}

Write-Host "Compilando Vox..." -ForegroundColor Cyan
dotnet publish "$root\Vox.csproj" -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $out
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Copy-Item "$root\config.json" -Destination $out -Force -ErrorAction SilentlyContinue
if (-not (Test-Path "$out\config.json")) {
    Copy-Item "$root\config.json" -Destination $out -Force
}

Copy-Item "$root\voice" -Destination $out -Recurse -Force

# Mantém apenas os arquivos de voz necessarios para o pt-BR
# (o espeak-ng-data completo traz ~100 idiomas e incha o instalador).
Prune-EspeakData -EspeakDir (Join-Path $out "voice\espeak-ng-data")

Write-Host "Pronto: $out\Vox.exe" -ForegroundColor Green

if ($Install) {
    $dest = Join-Path $env:LOCALAPPDATA "Vox"
    New-Item -ItemType Directory -Path $dest -Force | Out-Null

    $installedConfig = Join-Path $dest "config.json"
    if (-not (Test-Path $installedConfig)) {
        Copy-Item "$out\config.json" -Destination $dest -Force
    }
    Copy-Item "$out\Vox.exe", "$out\Vox.pdb", "$out\voice" -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue

    $startup = [Environment]::GetFolderPath("Startup")
    $lnk = Join-Path $startup "Vox.lnk"
    $ws = New-Object -ComObject WScript.Shell
    $sc = $ws.CreateShortcut($lnk)
    $sc.TargetPath = Join-Path $dest "Vox.exe"
    $sc.WorkingDirectory = $dest
    $sc.Save()
    Write-Host "Instalado e adicionado à inicialização do Windows." -ForegroundColor Green
}