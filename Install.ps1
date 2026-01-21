$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
Set-Location $scriptDir

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "Rad10\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net10.0\"

if (-not (Test-Path $consoleReleaseFolder))
{
    throw "folder $consoleReleaseFolder not found"
}


$optFolder = Join-Path -Path "/opt/" -ChildPath "Rad10"

if (-not (Test-Path $optFolder))
{
    throw "folder $optFolder not found"
}

Get-ChildItem -Path $optFolder -Recurse | Remove-Item -Verbose -Force -Recurse
Copy-Item -Path $consoleReleaseFolder/* -Destination $optFolder -Recurse -Force -Verbose

$appPath = Join-Path $optFolder -ChildPath "Rad10"
chmod +x $appPath