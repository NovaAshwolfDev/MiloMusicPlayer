@echo off
echo Building Release
dotnet build -c Release
start bin\Release\net10.0\MiloMusicPlayer.exe