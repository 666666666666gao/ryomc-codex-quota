$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$dll=Join-Path $root 'artifacts/Tommy.dll'
$sources=@('CodexConnection.cs','CredentialStore.cs','QuotaClient.cs','ConnectionSettings.cs','QuotaOverlay.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
foreach($test in @('ConfigTests','ConnectionTests')) {
    $exe=Join-Path $root "artifacts/$test.exe"
    & $compiler /nologo /target:exe "/main:$test" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Net.Http.dll /r:System.Web.Extensions.dll "/r:$dll" "/out:$exe" $sources (Join-Path $PSScriptRoot "$test.cs")
    if($LASTEXITCODE -ne 0) {throw 'Test build failed'}
    & $exe
    if($LASTEXITCODE -ne 0) {throw "$test failed"}
}
