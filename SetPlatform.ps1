Param($OS)

# OS_WINDOWS64
# OS_WINDOWS32
# OS_LINUX

function Set-Constant
{
    param
    (
        [Parameter(Mandatory = $true,ValueFromPipeline = $true)]
        [xml]$DABProjectConfig,

        $Target,

        $Value,

        [switch]$IncludeDefineConstants
    )
    process
    {
        Write-Host "Setting constant $Value for $Target"

        if ($IncludeDefineConstants)
        {
            $Value = ("`$(DefineConstants);" + $Value)
        }

        $propertyGroupNode = $null

        foreach ($node in  $DABProjectConfig | Select-Xml -XPath "/Project/PropertyGroup")
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
            throw "Node not found"
        }
        
        $constantsNode = $propertyGroupNode | Select-Xml -XPath "./DefineConstants"
        if ($constantsNode -eq $null)
        {
             $constantsNode = $DABProjectConfig.CreateElement("DefineConstants")
             $propertyGroupNode.Node.AppendChild($constantsNode) | Out-Null
             $constantsNode.InnerText = $Value
        } else
        {
            $constantsNode.Node.InnerText = $Value
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
            Write-Host "2) OS_WINDOWS32"
            Write-Host "3) OS_LINUX"
            $OSNumber = Read-Host

            switch ($OSNumber)
            {
                "1" { return "OS_WINDOWS64" }
                "2" { return "OS_WINDOWS32" }
                "3" { return "OS_LINUX" }
            }
       }                
    }
}

if ($OS -eq $null)
{
    $OS=Read-OS
}

$scriptDir = [System.IO.Path]::GetDirectoryName($MyInvocation.MyCommand.Path)

$DABProjectConfigFileName = Join-Path $scriptDir -ChildPath  "RTLSDR.DAB\RTLSDR.DAB.csproj"
[xml]$DABProjectConfig = Get-Content -Path $DABProjectConfigFileName

$DABProjectConfig | Set-Constant -Target "Debug|AnyCPU" -Value $OS -IncludeDefineConstants
$DABProjectConfig | Set-Constant -Target "Release|AnyCPU" -Value $OS

Write-Host "Saving $DABProjectConfigFileName"

$DABProjectConfig.Save($DABProjectConfigFileName)
