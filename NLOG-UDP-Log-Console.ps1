# PS script for listening NLOG messages when logging to UDP target using like this:
#   <target name="udp" xsi:type="NLogViewer" address="udp4://10.0.0.2:9999" layout="${longdate} ${uppercase:${level}}|${threadid}|${message}"/>

$port = 9999
$udpClient = New-Object System.Net.Sockets.UdpClient($port)
$endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)

Write-Host "Listening on UDP port $port..."

try 
{
    while ($true) 
    {
        $bytes = $udpClient.Receive([ref]$endpoint)
        $text  = [System.Text.Encoding]::UTF8.GetString($bytes)

        $sourceIP   = $endpoint.Address.ToString()

        # Inject missing namespace declaration
        $fixedXml = $text -replace '<log4j:event\b','<log4j:event xmlns:log4j="urn:log4j"'

        try 
        {
            $xml = [xml]$fixedXml
                        
            $time = [DateTimeOffset]::FromUnixTimeMilliseconds($xml.event.timestamp).LocalDateTime
            $time = $time.ToString("dd.MM.yyyy HH:mm:ss")            

            Write-Host  ("[" + $sourceIP + "] " + $time + " : " + $xml.event.message) 
        }
        catch 
        {        
            Write-Error $_.Exception
        }
    }
}
finally 
{
    $udpClient.Close()
}