$ErrorActionPreference = 'Stop'

dotnet .\PublishWindows.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

wsl ./PublishLinux.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet .\PublishAndroid.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet .\PublishChecksum.cs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }