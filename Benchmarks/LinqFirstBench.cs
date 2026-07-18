using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace LinqFirstProof.Benchmarks;

/// <summary>
/// First(предикат) и компания на .NET 8 / 9 / 10.
///
/// Честность замера:
///   - Полный проход: искомый элемент лежит ПОСЛЕДНИМ, его пара
///     (TargetX, TargetY) не встречается больше нигде - First обязан
///     просмотреть весь список. Значения остальных элементов случайные
///     с фиксированным seed, не повторы одного числа.
///   - Несколько длин: 100 (масштаб исходного репро - там 992 записи),
///     1000 и 100000.
///   - Assert совпадения: GlobalSetup проверяет, что все First-методы
///     возвращают ссылку на ОДИН И ТОТ ЖЕ объект, Count-методы - одно
///     число, SumControl - сумму checked-цикла. Иначе падает.
///
/// .NET 11 в BDN-бенчмарке нет (BenchmarkDotNet 0.15.8 его не собирает,
/// dotnet/BenchmarkDotNet#3017) - для него ручной харнесс:
/// Disasm.exe bench, одна методика на всех четырёх рантаймах.
/// </summary>
[MemoryDiagnoser(false)]
[SimpleJob(RuntimeMoniker.Net80)]
[SimpleJob(RuntimeMoniker.Net90, baseline: true)]
[SimpleJob(RuntimeMoniker.Net10_0)]
public class LinqFirstBench
{
    [Params(100, 1_000, 100_000)]
    public int Size { get; set; }

    private List<Item> _items = null!;
    private List<int> _numbers = null!;

    private int _targetX;
    private int _targetY;

    private int _countX;

    [GlobalSetup]
    public void Setup()
    {
        Random rng = new(12345);

        _items = new List<Item>(Size);
        for (int i = 0; i < Size - 1; i++)
        {
            // 0..1023 - заведомо меньше TargetX/TargetY, совпадений с целью нет
            _items.Add(new Item
            {
                X = rng.Next(1024),
                Y = rng.Next(1024)
            });
        }

        _items.Add(new Item
        {
            X = Subjects.TargetX,
            Y = Subjects.TargetY
        });

        _targetX = Subjects.TargetX;
        _targetY = Subjects.TargetY;

        _countX = 512;

        _numbers = new List<int>(Size);
        for (int i = 0; i < Size; i++)
        {
            _numbers.Add(rng.Next(16));
        }

        // Все методы обязаны согласиться в ответе, иначе не меряем.
        Item expected = _items[Size - 1];

        Check(nameof(Subjects.FirstFor), ReferenceEquals(Subjects.FirstFor(_items, _targetX, _targetY), expected));
        Check(nameof(Subjects.FirstForeach), ReferenceEquals(Subjects.FirstForeach(_items, _targetX, _targetY), expected));

        Check(nameof(Subjects.FirstLinq), ReferenceEquals(Subjects.FirstLinq(_items, _targetX, _targetY), expected));
        Check(nameof(Subjects.FirstLinqCached), ReferenceEquals(Subjects.FirstLinqCached(_items), expected));

        int expectedCount = Subjects.CountFor(_items, _countX);
        Check(nameof(Subjects.CountLinq), Subjects.CountLinq(_items, _countX) == expectedCount);

        int expectedSum = 0;
        for (int i = 0; i < _numbers.Count; i++)
        {
            checked
            {
                expectedSum += _numbers[i];
            }
        }

        Check(nameof(Subjects.SumControl), Subjects.SumControl(_numbers) == expectedSum);
    }

    private static void Check(string name, bool ok)
    {
        if (!ok)
        {
            throw new InvalidOperationException($"{name}: методы разошлись в ответе");
        }
    }

    [Benchmark(Baseline = true)]
    public Item First_Linq() => Subjects.FirstLinq(_items, _targetX, _targetY);

    [Benchmark]
    public Item First_LinqCached() => Subjects.FirstLinqCached(_items);

    [Benchmark]
    public Item First_For() => Subjects.FirstFor(_items, _targetX, _targetY);

    [Benchmark]
    public Item First_Foreach() => Subjects.FirstForeach(_items, _targetX, _targetY);

    [Benchmark]
    public int Count_Linq() => Subjects.CountLinq(_items, _countX);

    [Benchmark]
    public int Count_For() => Subjects.CountFor(_items, _countX);

    [Benchmark]
    public int Sum_Control() => Subjects.SumControl(_numbers);
}
