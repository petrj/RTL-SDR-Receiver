$scriptPath = $PSScriptRoot
cd $PSScriptRoot

foreach ($folder in `
    @(
    "RTLSDRReceiver\bin",
    "RTLSDRReceiver\obj",    
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
