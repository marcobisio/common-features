@echo off
ECHO *** RUNNING BUILD ***
dotnet run -p build\build.csproj %*
if %ERRORLEVEL% GEQ 1 GOTO :end
:end
pause