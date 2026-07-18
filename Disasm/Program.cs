using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using LinqFirstProof;

namespace Disasm;

/// <summary>
/// Три режима:
///
///   Disasm.exe          - паспорт железа + tiered-прогрев для снятия
///                         листингов под snap_pgo (методы прогреваются
///                         до Tier1 с живым PGO - именно там регресс)
///   Disasm.exe bench    - ручной харнесс: медиана нс/вызов по каждому
///                         методу. Нужен ради .NET 11, который
///                         BenchmarkDotNet 0.15.8 не гоняет: одна и та
///                         же методика на net8/9/10/11 - цифры сравнимы
///                         между собой. Это НЕ замена BDN на 8/9/10,
///                         это мост к одиннадцатому.
///
/// Смотреть в disasm_pgo_netN.txt: метод может печататься НЕСКОЛЬКО раз
/// (Tier0, instrumented, Tier1) - брать ПОСЛЕДНИЙ листинг, у него в
/// шапке "Tier1" и "with Dynamic PGO". В net9 внутри TryGetFirst
/// предикат заинлайнен (сравнения полей прямо в теле), в net10 -
/// call на делегат.
/// </summary>
internal static class Program
{
    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "bench")
        {
            Bench();
            return;
        }

        if (args.Length > 0 && args[0] == "osr")
        {
            Osr();
            return;
        }

        Console.WriteLine("=== Железо/рантайм ===");
        Console.WriteLine($"Avx2:     {Avx2.IsSupported}");
        Console.WriteLine($"Runtime:  {Environment.Version}");
        Console.WriteLine();

        (List<Item> items, List<int> numbers, int tx, int ty) = BuildData(1_000);

        // Прогрев до Tier1 + PGO: много вызовов, без выкрутасов.
        long sink = 0;
        for (int i = 0; i < 300_000; i++)
        {
            sink += Subjects.FirstLinq(items, tx, ty).X;
            sink += Subjects.FirstLinqCached(items).Y;
            sink += Subjects.FirstFor(items, tx, ty).X;
            sink += Subjects.FirstForeach(items, tx, ty).Y;
            sink += Subjects.CountLinq(items, 512);
            sink += Subjects.CountFor(items, 512);
            sink += Subjects.SumControl(numbers);
        }

        Console.WriteLine(sink);
    }

    // ------------------------------------------------------------------
    // Ручной харнесс. Схема: прогрев -> R повторов по M вызовов ->
    // медиана. Медиана, не среднее: срезает выбросы планировщика.
    // ------------------------------------------------------------------

    private static void Bench()
    {
        const int size = 1_000;
        const int repeats = 21;

        (List<Item> items, List<int> numbers, int tx, int ty) = BuildData(size);

        Console.WriteLine($"Runtime: {Environment.Version}, Size={size}, медиана из {repeats} повторов");
        Console.WriteLine();
        Console.WriteLine($"{"Метод",-18} {"нс/вызов",12}");

        Report("First_Linq", () => Subjects.FirstLinq(items, tx, ty).X, repeats);
        Report("First_LinqCached", () => Subjects.FirstLinqCached(items).X, repeats);
        Report("First_For", () => Subjects.FirstFor(items, tx, ty).X, repeats);
        Report("First_Foreach", () => Subjects.FirstForeach(items, tx, ty).X, repeats);
        Report("Count_Linq", () => Subjects.CountLinq(items, 512), repeats);
        Report("Count_For", () => Subjects.CountFor(items, 512), repeats);
        Report("Sum_Control", () => Subjects.SumControl(numbers), repeats);
    }

    private static void Report(string name, Func<int> action, int repeats)
    {
        // Прогрев до Tier1+PGO
        long sink = 0;
        for (int i = 0; i < 60_000; i++)
        {
            sink += action();
        }

        const int callsPerRepeat = 20_000;
        double[] results = new double[repeats];

        for (int r = 0; r < repeats; r++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            for (int i = 0; i < callsPerRepeat; i++)
            {
                sink += action();
            }

            sw.Stop();
            results[r] = sw.Elapsed.TotalNanoseconds / callsPerRepeat;
        }

        Array.Sort(results);
        double median = results[repeats / 2];

        Console.WriteLine($"{name,-18} {median,12:F1}   (контрольная сумма {sink})");
    }

    // ------------------------------------------------------------------
    // OSR-режим: один вызов = длинный внутренний цикл, метод уходит
    // в on-stack replacement - как Solve из issue #117717. Печатается
    // нс на одну итерацию внутреннего цикла, медиана по повторам.
    // ------------------------------------------------------------------

    private static void Osr()
    {
        const int size = 1_000;
        const int iterations = 200_000;
        const int repeats = 11;

        (List<Item> items, List<int> _, int tx, int ty) = BuildData(size);

        Console.WriteLine($"Runtime: {Environment.Version}, Size={size}, {iterations} итераций на вызов, медиана из {repeats}");
        Console.WriteLine();
        Console.WriteLine($"{"Метод",-10} {"нс/итерацию",14}");

        OsrReport("OsrLinq", () => Subjects.OsrLinq(items, tx, ty, iterations), iterations, repeats);
        OsrReport("OsrFor", () => Subjects.OsrFor(items, tx, ty, iterations), iterations, repeats);
    }

    private static void OsrReport(string name, Func<long> action, int iterations, int repeats)
    {
        long sink = action() + action();

        double[] results = new double[repeats];
        for (int r = 0; r < repeats; r++)
        {
            Stopwatch sw = Stopwatch.StartNew();
            sink += action();
            sw.Stop();
            results[r] = sw.Elapsed.TotalNanoseconds / iterations;
        }

        Array.Sort(results);
        Console.WriteLine($"{name,-10} {results[repeats / 2],14:F1}   (контрольная сумма {sink})");
    }

    private static (List<Item>, List<int>, int, int) BuildData(int size)
    {
        Random rng = new(12345);

        List<Item> items = new(size);
        for (int i = 0; i < size - 1; i++)
        {
            items.Add(new Item { X = rng.Next(1024), Y = rng.Next(1024) });
        }

        items.Add(new Item { X = Subjects.TargetX, Y = Subjects.TargetY });

        List<int> numbers = new(size);
        for (int i = 0; i < size; i++)
        {
            numbers.Add(rng.Next(16));
        }

        return (items, numbers, Subjects.TargetX, Subjects.TargetY);
    }
}
