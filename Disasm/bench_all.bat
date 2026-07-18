@echo off
cd /d %~dp0

dotnet build -c Release

rem Ручной харнесс на всех четырёх рантаймах - ради .NET 11.
bin\Release\net8.0\Disasm.exe  bench  > bench_net8.txt   2>&1
bin\Release\net9.0\Disasm.exe  bench  > bench_net9.txt   2>&1
bin\Release\net10.0\Disasm.exe bench  > bench_net10.txt  2>&1
if exist bin\Release\net11.0\Disasm.exe (bin\Release\net11.0\Disasm.exe bench > bench_net11.txt 2>&1)

type bench_net8.txt
type bench_net9.txt
type bench_net10.txt
if exist bench_net11.txt type bench_net11.txt
pause
