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
            BuildName    = $BuildName
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

    Add-Type -AssemblyName System.Drawing

    Add-Type @"
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

public static class IcoWriter
{
    public static void Write(string pngPath, string icoPath)
    {
        int[] sizes = new int[] { 16, 32, 48, 256 };
        byte[][] blobs = new byte[sizes.Length][];

        using (Bitmap source = new Bitmap(pngPath))
        {
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = sizes[i];
                using (Bitmap bmp = new Bitmap(size, size))
                using (Graphics g = Graphics.FromImage(bmp))
                using (MemoryStream ms = new MemoryStream())
                {
                    g.Clear(Color.Transparent);
                    g.CompositingQuality = CompositingQuality.HighQuality;
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, size, size);
                    bmp.Save(ms, ImageFormat.Png);
                    blobs[i] = ms.ToArray();
                }
            }

            using (FileStream fs = new FileStream(icoPath, FileMode.Create, FileAccess.Write))
            using (BinaryWriter bw = new BinaryWriter(fs))
            {
                bw.Write((ushort)0);
                bw.Write((ushort)1);
                bw.Write((ushort)sizes.Length);

                int offset = 6 + (16 * sizes.Length);

                for (int i = 0; i < sizes.Length; i++)
                {
                    int size = sizes[i];
                    byte width = (byte)(size >= 256 ? 0 : size);
                    byte height = (byte)(size >= 256 ? 0 : size);

                    bw.Write(width);
                    bw.Write(height);
                    bw.Write((byte)0);
                    bw.Write((byte)0);
                    bw.Write((ushort)1);
                    bw.Write((ushort)32);
                    bw.Write(blobs[i].Length);
                    bw.Write(offset);

                    offset += blobs[i].Length;
                }

                for (int i = 0; i < sizes.Length; i++)
                {
                    bw.Write(blobs[i]);
                }
            }
        }
    }
}
"@

    [IcoWriter]::Write($PngPath, $IcoPath)
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RootDir   = Resolve-Path (Join-Path $ScriptDir "..")
$DistDir   = Join-Path $RootDir "dist"
$GeneratedDir = Join-Path $RootDir "Builds/Generated/Windows"
$NsisDir   = Join-Path $GeneratedDir "nsis"

$Ctx = Get-BuildContext -Path $BuildDir
$BackupFolder = "新创Unity_BackUpThisFolder_ButDontShipItWithYourGame"
$BadRootFiles = @("BPLE_Setup.exe", "installer.nsi")
$IconSrc = Join-Path $RootDir "Assets/Texture2D/App Icon.png"
$IconOut = Join-Path $NsisDir "$($Ctx.OutputStem).ico"
$NsiOut = Join-Path $NsisDir "$($Ctx.OutputStem).nsi"
$OutFile = Join-Path $DistDir "$($Ctx.OutputStem)-setup.exe"

New-Item -ItemType Directory -Force -Path $DistDir | Out-Null
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
Write-Host "Built installer: $OutFile"