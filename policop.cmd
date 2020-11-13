@echo off
setlocal
set PROJECT_FILE=%~dp0src\policop\policop.csproj
dotnet run --project %PROJECT_FILE% -- %*
