#@cd /d %~dp0

$adbPath = "C:\Program Files (x86)\Android\android-sdk\platform-tools"

if (-not ($env:Path.Contains($adbPath)))
{
    $env:Path += ';C:\Program Files (x86)\Android\android-sdk\platform-tools'
}

$androidFolder="/storage/emulated/0/Android/media/net.petrjanousek.RTLSDRReceiver"

adb push "c:\temp\FM.raw" "$androidFolder/FM.raw"
adb push "c:\temp\DAB.raw" "$androidFolder/DAB.raw"