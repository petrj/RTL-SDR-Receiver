@cd /d %~dp0
set androidFolder=/storage/emulated/0/Android/media/net.petrjanousek.RTLSDRReceiver/
adb push c:\temp\FM.raw %androidFolder%FM.raw
adb push c:\temp\DAB.raw %androidFolder%DAB.raw