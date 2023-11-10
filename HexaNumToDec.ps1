function ConvertFrom-HexaStringNum
{
    param
    (
        [Parameter(Mandatory = $true,ValueFromPipeline = $true)]
        [string]$HexaStringNumber
    )
    process
    {
        $num = 0

        if ($HexaStringNumber.StartsWith("0x"))
        {
            $HexaStringNumber = $HexaStringNumber.Substring(2)
        }

        $nums = @()

        while ($HexaStringNumber.Length -ge 2)
        {
            $hexaNum = $HexaStringNumber.Substring(0,2)
            $dexNum = [convert]::toint64($hexaNum,16)
            $nums += $dexNum        
            $HexaStringNumber = $HexaStringNumber.Substring(2) 
        }

        [Array]::Reverse($nums)

        return [System.BitConverter]::ToUInt32($nums, 0)
    }
}

function ConvertTo-HexaStringNum
{
    param
    (
        [Parameter(Mandatory = $true,ValueFromPipeline = $true)]
        [uint32]$number
    )
    process
    {  
        $bytes = [System.BitConverter]::GetBytes($number)
        [Array]::Reverse($bytes)
        return ("0x" +[System.BitConverter]::ToString($bytes).Replace("-",""))
    }
}


"0x010636d658" | ConvertFrom-HexaStringNum
#1024000 | ConvertTo-HexaStringNum


