param([switch]$SkipTests, [string]$Version = '1.0.0')
$ErrorActionPreference = 'Stop'
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Use uma versão estável, por exemplo 1.0.1.' }
$nexusRoot = Split-Path $PSScriptRoot -Parent
$dotnetPath = Join-Path $env:LOCALAPPDATA 'NexusDev/dotnet/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnetPath)) { $dotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_ROOT = Split-Path $dotnetPath -Parent
$env:DOTNET_ROOT_X64 = $env:DOTNET_ROOT
$releaseDir = Join-Path $nexusRoot "dist/releases/$Version"
$payloadDir = Join-Path $nexusRoot ('native/artifacts/installer/' + [guid]::NewGuid().ToString('N'))
$packageDir = Join-Path $nexusRoot ('native/artifacts/packages/' + [guid]::NewGuid().ToString('N'))
$vpk = Join-Path $nexusRoot 'native/artifacts/tools/vpk.exe'
Push-Location $nexusRoot
try {
    if (-not $SkipTests) {
        & $dotnetPath run --project native/Nexus.Smoke/Nexus.Smoke.csproj -c Release
        if ($LASTEXITCODE -ne 0) { throw 'Os testes do núcleo falharam.' }
        & $dotnetPath build native/Nexus.Wpf/Nexus.Wpf.csproj -c Release "-p:Version=$Version" "-p:InformationalVersion=$Version" --nologo
        if ($LASTEXITCODE -ne 0) { throw 'A compilação WPF falhou.' }
        & $dotnetPath native/Nexus.Wpf/bin/Release/net10.0-windows/Nexus.dll --ui-test
        if ($LASTEXITCODE -ne 0) { throw 'Os testes da interface falharam. Consulte native/qa/ui-result.txt.' }
    }
    & $dotnetPath publish native/Nexus.Wpf/Nexus.Wpf.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false "-p:Version=$Version" "-p:InformationalVersion=$Version" -o native/artifacts/publish
    if ($LASTEXITCODE -ne 0) { throw 'A publicação do executável falhou.' }
    if (-not (Test-Path -LiteralPath $vpk)) {
        & $dotnetPath tool install vpk --version 1.2.161 --tool-path native/artifacts/tools
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível instalar o empacotador de atualizações.' }
    }
    & $vpk pack --packId Nexus.DatabaseStudio --packVersion $Version --packTitle Nexus --packAuthors WillianBN-Dis --packDir native/artifacts/publish --mainExe Nexus.exe --icon assets/nexus.ico --shortcuts None --channel win --runtime win-x64 --noInst --outputDir $packageDir
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível gerar os pacotes de atualização.' }
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
    Get-ChildItem -LiteralPath $packageDir -File | Copy-Item -Destination $releaseDir -Force
    $portable = Get-ChildItem -LiteralPath $releaseDir -Filter '*Portable.zip' | Select-Object -First 1
    if (-not $portable) { throw 'Pacote base do instalador não encontrado.' }
    if (-not $SkipTests) {
        & $dotnetPath run --project native/Nexus.UpdateSmoke/Nexus.UpdateSmoke.csproj -c Release -- $releaseDir $Version
        if ($LASTEXITCODE -ne 0) { throw 'Os testes de atualização falharam.' }
    }
    Expand-Archive -LiteralPath $portable.FullName -DestinationPath $payloadDir
    $compiler = Join-Path $env:LOCALAPPDATA 'Programs/Inno Setup 6/ISCC.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { $compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe' }
    if (-not (Test-Path -LiteralPath $compiler)) { $compiler = (Get-Command ISCC -ErrorAction Stop).Source }
    & $compiler "/DNexusVersion=$Version" "/DNexusPayload=$payloadDir" "/O$releaseDir" native/installer.iss
    if ($LASTEXITCODE -ne 0) { throw 'A geração do instalador falhou.' }
    $assets = @(Get-ChildItem -LiteralPath $releaseDir -Filter '*.nupkg') + @(Get-Item -LiteralPath "$releaseDir/releases.win.json", "$releaseDir/Nexus-Setup.exe")
    $assets | ForEach-Object { $hash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); "$hash  $($_.Name)" } | Set-Content -Encoding ascii -LiteralPath "$releaseDir/SHA256SUMS.txt"
    Copy-Item -LiteralPath "$releaseDir/Nexus-Setup.exe" -Destination dist/Nexus-Setup.exe -Force
    Write-Output "Instalador: $releaseDir/Nexus-Setup.exe"
    Write-Output "Atualizações: $releaseDir"
} finally { Pop-Location }
