#Requires -Version 5.1
[CmdletBinding()]
param(
    [ValidatePattern('^[A-Za-z0-9][A-Za-z0-9.-]*$')] [string]$HostName = 'kratos',
    [Parameter(Mandatory)] [ValidatePattern('^[A-Za-z0-9_.@-]+$')] [string]$UserName,
    [ValidatePattern('^[a-f0-9]{32}$')] [string]$RollbackRelease,
    [string]$IdentityFile
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$sshOptions = @('-o', 'BatchMode=yes')
if ($IdentityFile) { $sshOptions += @('-i', $IdentityFile) }

function Invoke-Checked {
    param([string]$Tool, [string[]]$Arguments)
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Tool exited with code $LASTEXITCODE." }
}

if ($RollbackRelease) {
    Invoke-Checked ssh ($sshOptions + @('-l', $UserName, $HostName,
        "sudo -n /usr/local/sbin/aegis-deploy-ffpmobile rollback $RollbackRelease"))
    return
}

$root = Split-Path -Parent $PSScriptRoot
$release = [Guid]::NewGuid().ToString('N')
$artifacts = Join-Path $root "artifacts\deploy\$release"
$output = Join-Path $artifacts 'app'
$bundle = Join-Path $artifacts 'app.tar.gz'
$remote = "/tmp/ffpmobile-$release.tar.gz"

Invoke-Checked dotnet @('test', (Join-Path $root 'tests/FFPMobile.Api.Tests'), '-c', 'Release',
    '--artifacts-path', $artifacts, '-m:1', '-p:UseSharedCompilation=false')
Invoke-Checked dotnet @('publish', (Join-Path $root 'services/FFPMobile.Api'), '-c', 'Release', '-r', 'linux-x64',
    '--self-contained', 'false', '--artifacts-path', $artifacts, '-o', $output, '-m:1', '-p:UseSharedCompilation=false')
Invoke-Checked tar @('-czf', $bundle, '-C', $output, '.')
Invoke-Checked scp ($sshOptions + @('-o', "User=$UserName", $bundle, "${HostName}:$remote"))
$remoteCommand = "trap 'rm -f -- $remote' EXIT; sudo -n /usr/local/sbin/aegis-deploy-ffpmobile deploy $release < $remote"
Invoke-Checked ssh ($sshOptions + @('-l', $UserName, $HostName, $remoteCommand))
Write-Host "Active release $release. Build and bundle retained in $artifacts."
