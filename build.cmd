@echo off
setlocal
set SLN_FILE=%~dp0src\Microsoft.DotnetOrg.Policies.sln
set OUT_DIR=%~dp0bin\
dotnet build %SLN_FILE% --nologo --property:OutputPath=%OUT_DIR% -- %*
