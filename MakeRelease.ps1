
$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

Set-Location $scriptDir

$Version = Get-Content -Path "version.txt"

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "RTLSDR.FMDAB.Console\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net8.0\"

$releaseFileName = "RTLSDR.FMDAB.Console"

./Clear.ps1
dotnet build $ConsoleProjectFolder\RTLSDR.FMDAB.Console.csproj --configuration=release -property:Version=$Version

if (($env:OS -ne $null) -and ($env:OS.StartsWith("Windows")))
{
    $releaseFileName += ("." + "win");
} else 
{
    $releaseFileName += ("." + "linux");
}

$releaseFileName += ".";
$releaseFileName += $Version;
$releaseFileName += ".zip";

$compress = @{
  Path = (Get-ChildItem -Path $consoleReleaseFolder -File | Select-Object -ExpandProperty "FullName")
  CompressionLevel = "Fastest"
  DestinationPath = "$releaseFileName"
}
Compress-Archive @compress -Force -Verbose

Write-Host "Saved to $releaseFileName"


$UNOProjectFolder = Join-Path -Path $scriptDir -ChildPath "RTLSDR.FMDAB.UNO\"
$UNOReleaseFolder = Join-Path -Path $UNOProjectFolder -ChildPath "bin\release\net8.0-desktop\"

$releaseFileName = "RTLSDR.FMDAB.UNO"

dotnet build $UNOProjectFolder\RTLSDR.FMDAB.UNO.csproj --configuration=release -property:Version=$Version

if (($env:OS -ne $null) -and ($env:OS.StartsWith("Windows")))
{
    $releaseFileName += ("." + "win");
} else 
{
    $releaseFileName += ("." + "linux");
}

$releaseFileName += ".";
$releaseFileName += $Version;
$releaseFileName += ".zip";

$compress = @{
  Path = (Get-ChildItem -Path $UNOReleaseFolder -Recurse | Select-Object -ExpandProperty "FullName")
  CompressionLevel = "Fastest"
  DestinationPath = "$releaseFileName"
}
Compress-Archive @compress -Force -Verbose

Write-Host "Saved to $releaseFileName"

