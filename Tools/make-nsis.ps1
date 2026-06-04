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
        $InstallRoot = if ($Arch -eq "64") { '$PROGRAMFILES64' } else { '$PROGRAMFILES' }

        return [pscustomobject]@{
            BuildPath    = $FullPath
            Version      = $Version
            PlatformArch = $PlatformArch
            InstallRoot  = $InstallRoot
            AppName      = "BPLE $Version"
            AppExe       = "新创Unity.exe"
            OutputStem   = "BPLE-$Version-windows-$PlatformArch"
        }
    }

    throw "Could not infer version/platform from build folder name: $BuildName"
}

function New-IcoFromPng {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PngPath,
        [Parameter(Mandatory = $true)]
        [string]$IcoPath
    )

    $Magick = Get-Command magick.exe -ErrorAction SilentlyContinue
    if (-not $Magick) {
        throw "ImageMagick 'magick' not found on PATH."
    }

    & $Magick.Source $PngPath -resize 512x512 $IcoPath
    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick failed while creating $IcoPath"
    }
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path (Join-Path $ScriptDir "..")
$PublishedDir = Join-Path $RootDir "Builds/Published"
$GeneratedDir = Join-Path $RootDir "Builds/Generated/Windows"
$NsisDir   = Join-Path $GeneratedDir "nsis"

$Ctx = Get-BuildContext -Path $BuildDir
$BackupFolder = "新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"
$IconSrc = Join-Path $RootDir "Assets/Texture2D/App Icon.png"
$IconOut = Join-Path $NsisDir "$($Ctx.OutputStem).ico"
$NsiOut = Join-Path $NsisDir "$($Ctx.OutputStem).nsi"
$OutFile = Join-Path $PublishedDir "$($Ctx.OutputStem)-setup.exe"

New-Item -ItemType Directory -Force -Path $PublishedDir | Out-Null
New-Item -ItemType Directory -Force -Path $GeneratedDir | Out-Null
New-Item -ItemType Directory -Force -Path $NsisDir | Out-Null

if (-not (Test-Path $IconSrc)) {
    throw "Icon not found: $IconSrc"
}

New-IcoFromPng -PngPath $IconSrc -IcoPath $IconOut

$nsi = @'
; (untested)
Unicode True
RequestExecutionLevel admin
SetCompressor /SOLID lzma

!define APP_NAME "__APP_NAME__"
!define APP_EXE "__APP_EXE__"
!define BUILD_DIR "__BUILD_DIR__"
!define OUT_FILE "__OUT_FILE__"
!define BACKUP_FOLDER "__BACKUP_FOLDER__"
!define INSTALL_ROOT "__INSTALL_ROOT__"
!define ICON_FILE "__ICON_FILE__"

Name "${APP_NAME}"
OutFile "${OUT_FILE}"
InstallDir "${INSTALL_ROOT}\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_NAME}" "InstallDir"
Icon "${ICON_FILE}"
UninstallIcon "${ICON_FILE}"

!cd "${BUILD_DIR}"

Page directory
Page instfiles

UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  File /r /x "${BACKUP_FOLDER}" /x "BPLE_Setup.exe" /x "installer.nsi" "*.*"
  WriteRegStr HKCU "Software\${APP_NAME}" "InstallDir" "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
  CreateShortcut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}"
SectionEnd

Section "Uninstall"
  Delete "$DESKTOP\${APP_NAME}.lnk"
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"
  DeleteRegKey HKCU "Software\${APP_NAME}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /r "$INSTDIR"
SectionEnd
'@

$nsi = $nsi.Replace("__APP_NAME__", $Ctx.AppName)
$nsi = $nsi.Replace("__APP_EXE__", $Ctx.AppExe)
$nsi = $nsi.Replace("__BUILD_DIR__", $Ctx.BuildPath)
$nsi = $nsi.Replace("__OUT_FILE__", $OutFile)
$nsi = $nsi.Replace("__BACKUP_FOLDER__", $BackupFolder)
$nsi = $nsi.Replace("__INSTALL_ROOT__", $Ctx.InstallRoot)
$nsi = $nsi.Replace("__ICON_FILE__", $IconOut)

Set-Content -Path $NsiOut -Value $nsi -Encoding UTF8

$Makensis = Get-Command makensis.exe -ErrorAction SilentlyContinue
if (-not $Makensis) {
    throw "makensis.exe not found on PATH."
}

& $Makensis.Source $NsiOut
if ($LASTEXITCODE -ne 0) {
    throw "NSIS failed with exit code $LASTEXITCODE"
}

Write-Host "Built installer: $OutFile"