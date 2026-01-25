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

# -f 8C -g 250 -sn "1175"

$sample = @"
#!/bin/bash
./RadI0 -f 8C -g 250 -sn "1175"
"@

$samplePath = Join-Path $optFolder -ChildPath "play_8C_service_1175_gain_250_example.sh"
$sample | Out-File -FilePath $samplePath
chmod +x $samplePath


# -fm -f 104Mhz"

$sample = @"
#!/bin/bash
./RadI0 -fm -f 104Mhz
"@

$samplePath = Join-Path $optFolder -ChildPath "play_FM_104MHz_example.sh"
$sample | Out-File -FilePath $samplePath
chmod +x $samplePath

# -f 8C

$sample = @"
#!/bin/bash
./RadI0 -f 8C
"@

$samplePath = Join-Path $optFolder -ChildPath "play_8C_example.sh"
$sample | Out-File -FilePath $samplePath
chmod +x $samplePath

Write-Host "Installation complete"