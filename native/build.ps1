param([switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$nexusRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $env:LOCALAPPDATA 'NexusDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
Push-Location $nexusRoot
try {
    if (-not $SkipTests) {
        & $dotnetPath run --project native/Nexus.Smoke/Nexus.Smoke.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Os testes do núcleo falharam.' }
        & $dotnetPath build native/Nexus.Wpf/Nexus.Wpf.csproj -c Release --nologo
        if ($LASTEXITCODE -ne 0) { throw 'A compilação WPF falhou.' }
        & $dotnetPath native/Nexus.Wpf/bin/Release/net10.0-windows/Nexus.dll --ui-test
        if ($LASTEXITCODE -ne 0) { throw 'Os testes da interface falharam. Consulte native/qa/ui-result.txt.' }
    }
    & $dotnetPath publish native/Nexus.Wpf/Nexus.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o native/artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'A publicação do executável falhou.' }
    $compiler = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { $compiler = (Get-Command ISCC -ErrorAction Stop).Source }
    & $compiler native/installer.iss
    if ($LASTEXITCODE -ne 0) { throw 'A geração do instalador falhou.' }
    Write-Output (Join-Path $nexusRoot 'dist/Nexus-Setup-0.6.0-wpf.4.exe')
} finally { Pop-Location }
