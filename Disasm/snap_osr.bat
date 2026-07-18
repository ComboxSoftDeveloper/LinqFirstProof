@echo off
cd /d %~dp0

dotnet build -c Release

rem Узкий фильтр: в дамп попадают ТОЛЬКО OSR-методы и TryGetFirst,
rem без остального. НИКАКОГО TieredCompilation=0 - регресс живёт
rem в связке OSR + PGO. В файле у OsrLinq будет несколько листингов
rem (Tier0 -> OSR -> Tier1) - смотреть OSR и последний Tier1:
rem есть ли call на Func:Invoke или предикат заинлайнен.
set DOTNET_JitDisasm=OsrLinq OsrFor *TryGetFirst*

bin\Release\net8.0\Disasm.exe  osr > osr_net8.txt   2>&1
bin\Release\net9.0\Disasm.exe  osr > osr_net9.txt   2>&1
bin\Release\net10.0\Disasm.exe osr > osr_net10.txt  2>&1
if exist bin\Release\net11.0\Disasm.exe (bin\Release\net11.0\Disasm.exe osr > osr_net11.txt 2>&1)

set DOTNET_JitDisasm=

echo Gotovo: osr_net8..11.txt - vnutri i zamer (ns/iteraciju), i disasm.
pause
