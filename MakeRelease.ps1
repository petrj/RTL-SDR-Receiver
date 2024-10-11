
$Version = "0.0.0.1"
$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

Set-Location $scriptDir

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "RTLSDR.FMDAB.Console\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net8.0\"
$consoleOutputFolder = Join-Path -Path $consoleReleaseFolder -ChildPath "RTLSDR.FMDAB.Console"

$releaseFileName = "RTLSDR.FMDAB.Console"

./Clear.ps1
dotnet build $ConsoleProjectFolder\RTLSDR.FMDAB.Console.csproj --configuration=release -property:Version=$Version

if ($env:OS.StartsWith("Windows"))
{
    $releaseFileName += ("." + "win");
}

$releaseFileName += ".";
$releaseFileName += $Version;
$releaseFileName += ".zip";

$filesToCompress = (Get-ChildItem -Path $consoleReleaseFolder -File | Select-Object -Property "FullName")


$compress = @{
  Path = (Get-ChildItem -Path $consoleReleaseFolder -File | Select-Object -ExpandProperty "FullName")
  CompressionLevel = "Fastest"
  DestinationPath = "$releaseFileName"
}
Compress-Archive @compress -Force -Verbose


 