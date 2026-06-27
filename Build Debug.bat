@echo off
echo Building Debug
dotnet build -c Debug
start bin\Debug\net10.0\MiloMusicPlayer.exe