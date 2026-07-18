# Порядок прогона

SDK как обычно: .NET 8, 9, 10 side-by-side, .NET 11 preview.

## 1. BDN-бенчмарк (каждая машина): .NET 8 / 9 / 10

```
dotnet run -c Release -f net10.0
```

Baseline - .NET 9 (регресс меряется относительно девятки). Результаты - BenchmarkDotNet.Artifacts/results.
Если GlobalSetup упал "методы разошлись в ответе" - не мерять, прислать текст.

## 2. Ручной харнесс (каждая машина): все ЧЕТЫРЕ рантайма, ради .NET 11

```
Disasm\bench_all.bat
```

Пишет bench_net8..11.txt и печатает их подряд. Это мост к одиннадцатому
(BDN 0.15.8 его не гоняет): методика одна на всех - цифры сравнимы между
рантаймами. Ответ на главный вопрос поста - "починили в 11 или нет" -
берётся отсюда: First_Linq net9 против net10 против net11.

## 3. Дизасм с живым PGO (хватит одной машины, лучше двух)

```
Disasm\snap_pgo.bat
```

НИКАКОГО TieredCompilation=0 - регресс живёт в Tier1 с PGO, без tiered
он исчезает. В disasm_pgo_netN.txt каждый метод печатается несколько раз
(Tier0 -> Instrumented -> Tier1) - смотреть ПОСЛЕДНИЙ листинг TryGetFirst,
в шапке "Tier1" и "optimized using Dynamic PGO". Что искать:
  net9  - предикат заинлайнен через guarded devirt: сравнения полей
          прямо в теле, call на Func:Invoke только в холодной ветке
  net10 - call [ ... ]Func`2:Invoke на каждой итерации
Старые и новые дампы не смешивать.

## Что прислать (по каждой машине)

- BenchmarkDotNet.Artifacts/results целиком
- bench_net8.txt .. bench_net11.txt
- disasm_pgo_net8..11.txt (с тех машин, где снимал)
- dotnet --list-sdks

## 4. OSR-сценарий (все машины)

```
Disasm\snap_osr.bat
```

Замер и дизасм одним запуском: в osr_netN.txt сверху цифры (нс/итерацию, медиана из 11), ниже листинги OsrLinq/OsrFor/TryGetFirst. Смотреть последний листинг метода: Tier1-OSR.
