@echo off
REM TapStrum Launcher
REM Uses dotnet run to handle MSIX deployment automatically

cd /d "%~dp0"
dotnet run --project TapStrum.Fluent.csproj
