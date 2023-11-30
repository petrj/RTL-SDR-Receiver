@cd /d %~dp0
set androidFolder=/storage/emulated/0/Android/media/net.petrjanousek.RTLSDRReceiver/
adb push c:\temp\test.raw %androidFolder%test.raw