
$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

Set-Location $scriptDir

$Version = Get-Content -Path "version.txt"

$radIOProjectFolder = Join-Path -Path $scriptDir -ChildPath "RadI0\"
$radIOReleaseFolder = Join-Path -Path $radIOProjectFolder -ChildPath "bin\release\net10.0\"

$releaseFileName = "RadI0"

./Clear.ps1
dotnet build $radIOProjectFolder\RadI0.csproj --configuration=release -property:Version=$Version

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
  Path = (Get-ChildItem -Path $radIOReleaseFolder -File | Select-Object -ExpandProperty "FullName")
  CompressionLevel = "Fastest"
  DestinationPath = "$releaseFileName"
}
Compress-Archive @compress -Force -Verbose 

Write-Host "Saved to $releaseFileName"
