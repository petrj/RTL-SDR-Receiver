cd $PSScriptRoot

$packageVersion = "1.0.1"

Write-Host "enter github access token: " -NoNewline
$token = Read-Host


function Publish-Project
{    
    [CmdletBinding()]
    param (
        [string]$ProjectName,
        [string]$PackageVersion,
        [string]$Token,
        [string]$PSScriptRoot
    )
    process 
    {
        $ProjectPath = Join-Path $PSScriptRoot -ChildPath "$ProjectName\$ProjectName.csproj"

        dotnet build $ProjectPath -c Release /p:PackageVersion=$packageVersion
        
        $fName = Join-Path $PSScriptRoot -ChildPath "$ProjectName\bin\release\$ProjectName.$PackageVersion.nupkg"
        dotnet nuget push $fName -k $Token --source "github" # --skip-duplicate
    }
}

# RTLSDR.Common

Publish-Project -ProjectName "RTLSDR.Common" -PackageVersion $packageVersion -PSScriptRoot $PSScriptRoot -Token $token
Publish-Project -ProjectName "RTLSDR" -PackageVersion $packageVersion -PSScriptRoot $PSScriptRoot -Token $token
Publish-Project -ProjectName "RTLSDR.FM" -PackageVersion $packageVersion -PSScriptRoot $PSScriptRoot -Token $token
Publish-Project -ProjectName "RTLSDR.DAB" -PackageVersion $packageVersion -PSScriptRoot $PSScriptRoot -Token $token


