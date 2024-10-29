Param($OS)

# OS_WINDOWS64
# OS_WINDOWS_MAUI
# OS_LINUX
# OS_ANADROID

function Set-Constant
{
    param
    (
        [Parameter(Mandatory = $true,ValueFromPipeline = $true)]
        [xml]$ProjectConfig,

        $Target,

        $Value,

        [switch]$IncludeDefineConstants,
        [switch]$IncludeTargetFramework
        )
    process
    {
        if ([String]::IsNullOrEmpty($Target))
        {
            Write-Host "Setting constant $Value"

            $node = $ProjectConfig | Select-Xml -XPath "/Project/PropertyGroup/DefineConstants"

            if ($node -ne $null)
            {
                $node.Node.InnerText = $Value
            } else 
            {
                $propertyGroupNode = $ProjectConfig | Select-Xml -XPath "/Project/PropertyGroup"
                $constNode = $ProjectConfig.CreateElement("DefineConstants")
                $constNode.InnerText = $Value
                $propertyGroupNode.Node.AppendChild($constNode) | Out-Null
            }

            return;
        }

        Write-Host "Setting constant $Value for $Target"

        if ($IncludeDefineConstants)
        {
            $Value = ("`$(DefineConstants);" + $Value)
        }

        $propertyGroupNode = $null

        foreach ($node in  $ProjectConfig | Select-Xml -XPath "/Project/PropertyGroup")
        {
            $conditionAttribute = $node.Node.GetAttribute("Condition")
            if (($conditionAttribute -ne $null) -and ($conditionAttribute.Contains($Target)))
            {
                $propertyGroupNode = $node
                break;
            }
        }

        if ($propertyGroupNode -eq $null)
        {
            # "Node not found"
            $projectNode = $ProjectConfig | Select-Xml -XPath "/Project"
            $propertyGroupNode = $ProjectConfig.CreateElement("PropertyGroup")
            $projectNode.Node.AppendChild($propertyGroupNode) | Out-Null
            if ($IncludeTargetFramework)
            {
                $propertyGroupNode.SetAttribute("Condition","`'`$(Configuration)|`$(TargetFramework)|`$(Platform)`'==`'" + $Target + "`'")
            } else
            {
                $propertyGroupNode.SetAttribute("Condition","`'`$(Configuration)|`$(Platform)`'==`'" + $Target + "`'")
            }
            
            $propertyGroupNode.InnerXml = ("<DefineConstants>" + $Value + "</DefineConstants>")
        } else
        {        
            $constantsNode = $propertyGroupNode | Select-Xml -XPath "./DefineConstants"
            if ($constantsNode -eq $null)
            {
                 $constantsNode = $ProjectConfig.CreateElement("DefineConstants")
                 $propertyGroupNode.Node.AppendChild($constantsNode) | Out-Null            
                 $constantsNode.InnerText = $Value
            } else
            {
                $constantsNode.Node.InnerText = $Value
            }
        }        
    }
}


function Read-OS
{
    param
    (
       
    )
    process
    {
        

       $ok = $false
       while (!$ok)
       {
            Write-Host "Set OS/PLatform:"
            Write-Host "1) OS_WINDOWS64"
            Write-Host "2) OS_WINDOWS_MAUI"
            Write-Host "3) OS_LINUX"
            Write-Host "4) OS_ANDROID"
            $OSNumber = Read-Host

            switch ($OSNumber)
            {
                "1" { return "OS_WINDOWS64" }
                "2" { return "OS_WINDOWS_MAUI" }
                "3" { return "OS_LINUX" }
                "4" { return "OS_ANDROID" }
            }
       }                
    }
}

if ($OS -eq $null)
{
    $OS=Read-OS
}

$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

# RTLSDR.DAB

Write-Host "RTLSDR.DAB" -ForegroundColor Yellow

$DABProjectConfigFileName = Join-Path $scriptDir -ChildPath  "RTLSDR.DAB\RTLSDR.DAB.csproj"
[xml]$DABProjectConfig = Get-Content -Path $DABProjectConfigFileName

$DABProjectConfig | Set-Constant -Target "Debug|AnyCPU" -Value $OS -IncludeDefineConstants
$DABProjectConfig | Set-Constant -Target "Release|AnyCPU" -Value $OS

Write-Host "Saving $DABProjectConfigFileName"

$DABProjectConfig.Save($DABProjectConfigFileName)

# RTLSDR.FMDAB.MAUI

Write-Host "RTLSDR.FMDAB.MAUI" -ForegroundColor Yellow

$MAUIProjectConfigFileName = Join-Path $scriptDir -ChildPath  "RTLSDR.FMDAB.MAUI\RTLSDR.FMDAB.MAUI.csproj"
[xml]$MAUIProjectConfig = Get-Content -Path $MAUIProjectConfigFileName

$MAUIProjectConfig | Set-Constant -Target "Debug|net8.0-android|AnyCPU" -Value $OS -IncludeDefineConstants -IncludeTargetFramework
$MAUIProjectConfig | Set-Constant -Target "Debug|net8.0-windows10.0.26100.0|AnyCPU" -Value $OS -IncludeDefineConstants -IncludeTargetFramework

$MAUIProjectConfig | Set-Constant -Target "Release|net8.0-android|AnyCPU" -Value $OS -IncludeDefineConstants -IncludeTargetFramework
$MAUIProjectConfig | Set-Constant -Target "Release|net8.0-windows10.0.26100.0|AnyCPU" -Value $OS -IncludeDefineConstants -IncludeTargetFramework

$MAUIProjectConfig.Save($MAUIProjectConfigFileName)
