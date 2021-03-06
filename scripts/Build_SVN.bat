@echo off
cls
Title Building MediaPortal Fanart Handler (RELEASE)
cd ..

if "%programfiles(x86)%XXX"=="XXX" goto 32BIT
	:: 64-bit
	set PROGS=%programfiles(x86)%
	goto CONT
:32BIT
	set PROGS=%ProgramFiles%
:CONT

: Prepare version
subwcrev . FanartHandler\Properties\AssemblyInfo.cs FanartHandler\Properties\AssemblyInfo.cs
	
:: Build
"%WINDIR%\Microsoft.NET\Framework\v4.0.30319\MSBUILD.exe" /target:Rebuild /property:Configuration=RELEASE /fl /flp:logfile=FanartHandler.log;verbosity=diagnostic FanartHandler.sln

: Revert version
svn revert FanartHandler\Properties\AssemblyInfo.cs

cd scripts

pause

