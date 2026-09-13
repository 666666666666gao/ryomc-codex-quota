$ErrorActionPreference = 'Stop'
$overlayRoot = Split-Path -Parent $PSScriptRoot
$overlayOutput = Join-Path $overlayRoot 'artifacts'
New-Item -ItemType Directory -Path $overlayOutput -Force | Out-Null
# Pinned TOML parser: real config files contain nested tables, comments and escaped strings.
$parserPackage = Join-Path $overlayOutput 'Tommy.3.1.2.nupkg'
if (-not (Test-Path -LiteralPath $parserPackage)) {
    Invoke-WebRequest 'https://github.com/dezhidki/Tommy/releases/download/v3.1.2/Tommy.3.1.2.nupkg' -OutFile $parserPackage
}
if ((Get-FileHash -LiteralPath $parserPackage -Algorithm SHA256).Hash -ne '344D4C2F63D901F969114B1D6C3F6458A9A14E0B46B89016742748E75B81947F') { throw 'TOML parser checksum mismatch' }
$parserDirectory = Join-Path $overlayOutput 'tommy-3.1.2'
if (-not (Test-Path -LiteralPath $parserDirectory)) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [IO.Compression.ZipFile]::ExtractToDirectory($parserPackage, $parserDirectory)
}
$parserDll = Join-Path $overlayOutput 'Tommy.dll'
Copy-Item -LiteralPath (Join-Path $parserDirectory 'lib/net35/Tommy.dll') -Destination $parserDll -Force
Copy-Item -LiteralPath (Join-Path $parserDirectory 'LICENSE') -Destination (Join-Path $overlayOutput 'Tommy-LICENSE.txt') -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$overlaySource = Join-Path $PSScriptRoot 'QuotaOverlay.cs'
$configSource = Join-Path $PSScriptRoot 'CodexConnection.cs'
$credentialSource = Join-Path $PSScriptRoot 'CredentialStore.cs'
$settingsSource = Join-Path $PSScriptRoot 'ConnectionSettings.cs'
$clientSource = Join-Path $PSScriptRoot 'QuotaClient.cs'
$overlayBinary = Join-Path $overlayOutput 'RyomcQuota.exe'
& $compiler /nologo /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll "/r:$parserDll" "/out:$overlayBinary" $overlaySource $configSource $credentialSource $settingsSource $clientSource
if ($LASTEXITCODE -ne 0) { throw 'Overlay compilation failed' }
Write-Output (Join-Path $overlayOutput 'RyomcQuota.exe')
