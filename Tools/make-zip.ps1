# (untested)
param(
    [Parameter(Mandatory = $true)]
    [string]$BuildDir
)

$ErrorActionPreference = "Stop"

function Get-BuildContext {
    param([string]$Path)

    $FullPath = (Resolve-Path $Path).Path
    $BuildName = Split-Path $FullPath -Leaf

    if ($BuildName -match '^BPLE\s+(?<Version>.+)\s+Windows\s+(?<Arch>32|64)\s+Bits$') {
        $Version = $Matches.Version
        $Arch = $Matches.Arch
        $PlatformArch = if ($Arch -eq "64") { "x64" } else { "x86" }

        return [pscustomobject]@{
            BuildPath    = $FullPath
            Version      = $Version
            PlatformArch = $PlatformArch
            OutputStem   = "BPLE-$Version-windows-$PlatformArch"
        }
    }

    throw "Could not infer version/platform from build folder name: $BuildName"
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path (Join-Path $ScriptDir "..")
$PublishedDir = Join-Path $RootDir "Builds/Published"
$GeneratedDir = Join-Path $RootDir "Builds/Generated/Windows"
$StageDir  = Join-Path $GeneratedDir "zip-stage"

$Ctx = Get-BuildContext -Path $BuildDir
$BackupFolder = "新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"
$BadRootFiles = @("BPLE_Setup.exe", "installer.nsi")

New-Item -ItemType Directory -Force -Path $PublishedDir | Out-Null
New-Item -ItemType Directory -Force -Path $GeneratedDir | Out-Null

if (Test-Path $StageDir) {
    Remove-Item $StageDir -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $StageDir | Out-Null

robocopy $Ctx.BuildPath $StageDir /MIR /XD $BackupFolder /XF $BadRootFiles /NFL /NDL /NJH /NJS /NP | Out-Null
if ($LASTEXITCODE -ge 8) {
    throw "robocopy failed with exit code $LASTEXITCODE"
}

$ZipPath = Join-Path $PublishedDir "$($Ctx.OutputStem).zip"
if (Test-Path $ZipPath) {
    Remove-Item $ZipPath -Force
}

Compress-Archive -Path (Join-Path $StageDir "*") -DestinationPath $ZipPath -CompressionLevel Optimal

Remove-Item $StageDir -Recurse -Force
Write-Host "Built ZIP: $ZipPath"