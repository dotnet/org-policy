@echo off
setlocal
set SLN_FILE=%~dp0src\Microsoft.DotnetOrg.Policies.sln
dotnet build %SLN_FILE% --nologo -- %*
