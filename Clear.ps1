$scriptPath = $PSScriptRoot
cd $PSScriptRoot

foreach ($folder in `
    @(
    "packages",
    "LoggerService\bin",
    "LoggerService\obj",
    "RTLSDR\bin",
    "RTLSDR\obj",
    "RTLSDR.Audio\bin",
    "RTLSDR.Audio\obj",    
    "RTLSDR.Common\bin",
    "RTLSDR.Common\obj",    
    "RTLSDR.FM\bin",
    "RTLSDR.FM\obj",
    "RTLSDR.DAB\bin",
    "RTLSDR.DAB\obj",
    "RTLSDR.FMDAB.Console\bin",
    "RTLSDR.FMDAB.Console\obj",
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

