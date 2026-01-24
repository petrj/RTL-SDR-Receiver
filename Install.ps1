$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)
Set-Location $scriptDir

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "RadI0\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net10.0\"

if (-not (Test-Path $consoleReleaseFolder))
{
    throw "folder $consoleReleaseFolder not found"
}


$optFolder = Join-Path -Path "/opt/" -ChildPath "RadI0"

if (-not (Test-Path $optFolder))
{
    throw "folder $optFolder not found"
}

Get-ChildItem -Path $optFolder -Recurse | Remove-Item -Force -Recurse
Copy-Item -Path $consoleReleaseFolder/* -Destination $optFolder -Recurse -Force

$appPath = Join-Path $optFolder -ChildPath "RadI0"
chmod +x $appPath

Write-Host "Installation complete"