[CmdletBinding()]
param(
    [ValidateSet('all', 'centralized', 'edge', 'distributed-center')]
    [string]$Role = 'all',
    [string]$Iscc = ''
)

$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build-roles.ps1') @PSBoundParameters
