# Compatibility entry point for existing automation.
[CmdletBinding()]
param([string]$Destination)

& (Join-Path $PSScriptRoot 'Export-UIPrefabGenerator.ps1') @PSBoundParameters
