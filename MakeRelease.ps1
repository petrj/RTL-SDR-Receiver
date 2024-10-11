
$RTL_SDR_Win_folder = "C:\Program Files\rtl-sdr-64bit-20241006\"

$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

Set-Location $scriptDir

$consoleProjectFolder = Join-Path -Path $scriptDir -ChildPath "RTLSDR.FMDAB.Console\"
$consoleReleaseFolder = Join-Path -Path $consoleProjectFolder -ChildPath "bin\release\net8.0\"


./Clear.ps1
dotnet build $ConsoleProjectFolder\RTLSDR.FMDAB.Console.csproj --configuration=release

if ($Env:OS.StartsWith("Windows"))
{
    foreach ( $f in $("librtlsdr.dll","libusb-1.0.dll","libwinpthread-1.dll","rtl_tcp.exe"))
    {
        Copy-Item -Path (Join-Path -Path $RTL_SDR_Win_folder -ChildPath $f) -Destination (Join-Path -Path $consoleReleaseFolder -ChildPath $f)
    }
}

