@echo off

:: Register action is for extension Mp3CoverDroper.Extension's dll
cd Mp3CoverDroper.Extension

:: Generate Key
cd bin\x64\Release
sn -k key.snk

:: Recompile
ildasm Mp3CoverDroper.Extension.dll /OUTPUT=Mp3CoverDroper.Extension.il
ilasm Mp3CoverDroper.Extension.il /DLL /OUTPUT=Mp3CoverDroper.Extension.dll /KEY=key.snk

:: Backup dll
if not exist "..\..\..\build" mkdir ..\..\..\build
cp Mp3CoverDroper.Extension.dll     ..\..\..\build\Mp3CoverDroper.Extension.dll
cp SharpShell.dll                   ..\..\..\build\SharpShell.dll
cd ..\..\..\build

:: Register
regasm /codebase Mp3CoverDroper.Extension.dll

:: Back to solution folder
cd ..\..

@echo on
