$scriptPath = $PSScriptRoot
cd $PSScriptRoot

foreach ($folder in `
    @(
    "packages",
    "FMDAB.Core\bin",
    "FMDAB.Core\obj",
    "FM\bin",
    "FM\obj",
    "LoggerService\bin",
    "LoggerService\obj",
    "DAB\bin",
    "DAB\obj",
    "RTLSDR\bin",
    "RTLSDR\obj",
	"RTLSDR.FMDAB.Console.Common\bin",
    "RTLSDR.FMDAB.Console.Common\obj",
    "RTLSDR.FMDAB.Console.32\bin",
    "RTLSDR.FMDAB.Console.32\obj",
    "RTLSDR.FMDAB.Console.64\bin",
    "RTLSDR.FMDAB.Console.64\obj",
    "RTLSDR.FMDAB.Console.Common\bin",
    "RTLSDR.FMDAB.Console.Common\obj",
	"RTLSDR.FMDAB.MAUI\bin",
    "RTLSDR.FMDAB.MAUI\obj",
    "Tests\bin",
    "Tests\obj",
    ".vs"
     ))
{
    $fullPath = [System.IO.Path]::Combine($scriptPath,$folder)
    
    if (-not $fullPath.EndsWith("\"))
    {
            $fullPath += "\"
    }

    if (Test-Path -Path $fullPath)
    {
	Remove-Item -Path $fullPath -Recurse -Force -Verbose		
    }
}

