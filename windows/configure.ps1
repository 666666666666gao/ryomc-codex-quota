$ErrorActionPreference = 'Stop'
$readerEndpoint = Read-Host 'HTTPS quota endpoint (Enter for https://api.ryomc.top/quota/weekly)'
if ([string]::IsNullOrWhiteSpace($readerEndpoint)) { $readerEndpoint = 'https://api.ryomc.top/quota/weekly' }
$readerUri = [uri]$readerEndpoint
if ($readerUri.Scheme -ne 'https' -or $readerUri.UserInfo -or $readerUri.Query -or $readerUri.Fragment) { throw 'Use an HTTPS endpoint without credentials, query or fragment.' }
$readerSecret = Read-Host 'Read-only quota token (NOT CPA management key)' -AsSecureString
$readerPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($readerSecret)
try {
    $readerToken = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($readerPointer)
    if ([string]::IsNullOrWhiteSpace($readerToken)) { throw 'Read-only token is required.' }
    $readerDirectory = Join-Path ([Environment]::GetFolderPath('UserProfile')) '.config/ryomc-codex-quota'
    New-Item -ItemType Directory -Path $readerDirectory -Force | Out-Null
    $readerUser = (whoami).Trim()
    & icacls $readerDirectory /inheritance:r /grant:r "${readerUser}:(OI)(CI)F" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not restrict configuration directory permissions.' }
    $readerConfig = @{endpoint=$readerUri.AbsoluteUri; readToken=$readerToken} | ConvertTo-Json
    [IO.File]::WriteAllText((Join-Path $readerDirectory 'reader.json'), $readerConfig, (New-Object Text.UTF8Encoding($false)))
    Write-Output 'Saved. Start RyomcQuota.exe and bring Codex to the foreground.'
} finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($readerPointer)
    $readerToken = $null
    $readerConfig = $null
}
