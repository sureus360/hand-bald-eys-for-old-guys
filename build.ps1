[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Publish
)

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$solution = Join-Path $projectRoot 'HandheldZoom.sln'
$artifacts = Join-Path $projectRoot 'artifacts'
$localDotnet = Join-Path $projectRoot '.dotnet\dotnet.exe'
$dotnet = if (Test-Path $localDotnet) { $localDotnet } else { 'dotnet' }

Push-Location $projectRoot
try {
    & $dotnet restore $solution -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw 'Restore fehlgeschlagen.' }
    & $dotnet build $solution -c $Configuration -p:Platform=x64 --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Build fehlgeschlagen.' }
    & $dotnet run --project 'tests/HandheldZoom.Core.Tests/HandheldZoom.Core.Tests.csproj' -c $Configuration -p:Platform=x64 --no-build

    if ($LASTEXITCODE -ne 0) { throw 'Tests fehlgeschlagen.' }
    if ($Publish) {
        $testOutput = Join-Path $artifacts 'magnification-test'
        $recoveryOutput = Join-Path $artifacts 'recovery'
        & $dotnet publish 'src/HandheldZoom.MagnificationTest/HandheldZoom.MagnificationTest.csproj' -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $testOutput
        if ($LASTEXITCODE -ne 0) { throw 'Test-Publish fehlgeschlagen.' }
        & $dotnet publish 'src/HandheldZoom.Recovery/HandheldZoom.Recovery.csproj' -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o $recoveryOutput
        if ($LASTEXITCODE -ne 0) { throw 'Recovery-Publish fehlgeschlagen.' }
        & $dotnet publish 'src/HandheldZoom.QuickZoom/HandheldZoom.QuickZoom.csproj' -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -o (Join-Path $artifacts 'quick-zoom')
        if ($LASTEXITCODE -ne 0) { throw 'QuickZoom-Publish fehlgeschlagen.' }
        $desktopOutput = Join-Path $artifacts 'desktop'
        & $dotnet publish 'src/HandheldZoom.Desktop/HandheldZoom.Desktop.csproj' -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $desktopOutput
        if ($LASTEXITCODE -ne 0) { throw 'Desktop-Publish fehlgeschlagen.' }
        $appOutput = Join-Path $artifacts 'app'
        New-Item -ItemType Directory -Path $appOutput -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $desktopOutput 'HandheldZoom.Desktop.exe') -Destination (Join-Path $appOutput 'hand bald eys for old guys.exe') -Force
        Copy-Item -LiteralPath (Join-Path $recoveryOutput 'HandheldZoom.Recovery.exe') -Destination (Join-Path $appOutput 'HandheldZoom.Recovery.exe') -Force
        Copy-Item -LiteralPath (Join-Path $projectRoot 'src/HandheldZoom.Desktop/BEDIENUNG.txt') -Destination (Join-Path $appOutput 'BEDIENUNG.txt') -Force
        Write-Host "Portable Ausgabe: $artifacts"
    }
}
finally {
    Pop-Location
}
