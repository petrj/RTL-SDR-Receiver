$scriptPath = $PSScriptRoot
cd $PSScriptRoot

foreach ($folder in `
    @(
    "packages",
    "Core\bin",
    "Core\obj",
    "FM\bin",
    "FM\obj",
    "LoggerService\bin",
    "LoggerService\obj",
    "DAB\bin",
    "DAB\obj",
    "RTLSDR\bin",
    "RTLSDR\obj",
    "RTLSDRReceiver\bin",
    "RTLSDRReceiver\obj",
    "RTLSDRConsole\bin",
    "RTLSDRConsole\obj",
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
