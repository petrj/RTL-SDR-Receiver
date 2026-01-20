
$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

Set-Location $scriptDir

$Version = Get-Content -Path "version.txt"

$rad10ProjectFolder = Join-Path -Path $scriptDir -ChildPath "Rad10\"
$rad10ReleaseFolder = Join-Path -Path $rad10ProjectFolder -ChildPath "bin\release\net10.0\"

$releaseFileName = "Rad10"

./Clear.ps1
dotnet build $rad10ProjectFolder\Rad10.csproj --configuration=release -property:Version=$Version

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
  Path = (Get-ChildItem -Path $rad10ReleaseFolder -File | Select-Object -ExpandProperty "FullName")
  CompressionLevel = "Fastest"
  DestinationPath = "$releaseFileName"
}
Compress-Archive @compress -Force -Verbose 

Write-Host "Saved to $releaseFileName"
