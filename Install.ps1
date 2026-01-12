$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
Set-Location $scriptDir

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "RTLSDR.FMDAB.Console\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net10.0\"

if (-not (Test-Path $consoleReleaseFolder))
{
    throw "folder $consoleReleaseFolder not found"
}


$optFolder = Join-Path -Path "/opt/" -ChildPath "RTLSDR.FMDAB.Console"

if (-not (Test-Path $optFolder))
{
    throw "folder $optFolder not found"
}

Get-ChildItem -Path $optFolder -Recurse | Remove-Item -Verbose -Force -Recurse
Copy-Item -Path $consoleReleaseFolder/* -Destination $optFolder -Recurse -Force -Verbose

$appPath = Join-Path $optFolder -ChildPath "RTLSDR.FMDAB.Console"
chmod +x $appPath