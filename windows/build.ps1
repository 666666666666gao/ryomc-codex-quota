$ErrorActionPreference = 'Stop'
$overlayRoot = Split-Path -Parent $PSScriptRoot
$overlayOutput = Join-Path $overlayRoot 'artifacts'
New-Item -ItemType Directory -Path $overlayOutput -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$overlaySource = Join-Path $PSScriptRoot 'QuotaOverlay.cs'
$overlayBinary = Join-Path $overlayOutput 'RyomcQuota.exe'
& $compiler /nologo /target:winexe /optimize+ /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll "/out:$overlayBinary" $overlaySource
if ($LASTEXITCODE -ne 0) { throw 'Overlay compilation failed' }
Write-Output (Join-Path $overlayOutput 'RyomcQuota.exe')
