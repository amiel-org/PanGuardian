[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$Message
)

$ErrorActionPreference = 'Stop'

function Invoke-GitCommand {
    param([string[]]$GitArgs)

    & git @GitArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed: git $($GitArgs -join ' ')"
    }
}

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    throw 'Git is not installed or is not available on PATH.'
}

Set-Location -LiteralPath $PSScriptRoot

$changes = & git status --porcelain
if ($LASTEXITCODE -ne 0) {
    throw 'This directory is not a valid Git repository.'
}

if (-not $changes) {
    Write-Host 'No local changes to upload.'
    exit 0
}

Invoke-GitCommand @('add', '--all')
Invoke-GitCommand @('commit', '-m', $Message)
Invoke-GitCommand @('pull', '--rebase')
Invoke-GitCommand @('push')

Write-Host 'GitHub update completed.'
