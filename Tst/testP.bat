echo off
cd ..\Bld
call build.bat -d
cd ..\Tst

set bldexe = ""

if exist "%WinDir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe" (
  set bldexe="%WinDir%\Microsoft.NET\Framework\v4.0.30319\msbuild.exe"
) else (
  echo Microsoft.NET framework version 4.0 or greater is required. Cannot build.
  exit /B 1)

echo Bootstrapping the test utility...
%bldexe%  .\Tools\RunPTool\RunPTool.csproj /t:Clean /p:Configuration=Debug /p:Platform=x86 /verbosity:quiet /nologo
if %ERRORLEVEL% neq 0 (
  echo Could not clean test utility.
  exit /B 1
)

%bldexe%  .\Tools\RunPTool\RunPTool.csproj /t:Build /p:Configuration=Debug /p:Platform=x86 /verbosity:quiet /nologo
if %ERRORLEVEL% neq 0 (
  echo Could not compile test utility.
  exit /B 1
)

.\Tools\RunPTool\bin\x86\Debug\RunPTool.exe %1 %2 %3
if %ERRORLEVEL% neq 0 (
  echo Tests failed.
  exit /B 1
) else ( exit /B 0 )