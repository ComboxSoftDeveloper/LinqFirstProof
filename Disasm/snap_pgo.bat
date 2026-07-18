@echo off
cd /d %~dp0

dotnet build -c Release

rem ВАЖНО: НИКАКОГО TieredCompilation=0 - регресс живёт в Tier1 с PGO.
rem Метод печатается несколько раз (Tier0, instrumented, Tier1) -
rem в файле смотреть ПОСЛЕДНИЙ листинг: "Tier1" + "with Dynamic PGO".
set DOTNET_JitDisasm=*TryGetFirst* FirstLinq FirstLinqCached FirstFor CountLinq

bin\Release\net8.0\Disasm.exe  > disasm_pgo_net8.txt   2>&1
bin\Release\net9.0\Disasm.exe  > disasm_pgo_net9.txt   2>&1
bin\Release\net10.0\Disasm.exe > disasm_pgo_net10.txt  2>&1
if exist bin\Release\net11.0\Disasm.exe (bin\Release\net11.0\Disasm.exe > disasm_pgo_net11.txt 2>&1)

set DOTNET_JitDisasm=

echo Gotovo. V net9 predikat vnutri TryGetFirst inlinen, v net10 - call na delegat.
pause
