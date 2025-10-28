﻿cd $PSScriptRoot

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

#Publish-Project -ProjectName "RTLSDR.Common" -PackageVersion "1.0.1" -PSScriptRoot $PSScriptRoot -Token $token
#Publish-Project -ProjectName "RTLSDR" -PackageVersion "1.0.3" -PSScriptRoot $PSScriptRoot -Token $token
#Publish-Project -ProjectName "RTLSDR.FM" -PackageVersion "1.0.1" -PSScriptRoot $PSScriptRoot -Token $token
#Publish-Project -ProjectName "RTLSDR.DAB" -PackageVersion "1.0.1" -PSScriptRoot $PSScriptRoot -Token $token
Publish-Project -ProjectName "RTLSDR.Audio" -PackageVersion "1.0.1" -PSScriptRoot $PSScriptRoot -Token $token


