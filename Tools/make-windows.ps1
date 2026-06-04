# (untested)
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildDir
)

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

& (Join-Path $ScriptDir "make-zip.ps1") -BuildDir $BuildDir
& (Join-Path $ScriptDir "make-nsis.ps1") -BuildDir $BuildDir