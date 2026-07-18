using System.Runtime.CompilerServices;

namespace LinqFirstProof;

/// <summary>
/// First(предикат) по List: свой цикл против LINQ на .NET 8/9/10/11.
///
/// Ключевое: в .NET 10 First с предикатом отстаёт от .NET 9 - на Intel
/// в исходном репро до 19%. Причина названа JIT-командой: связка PGO
/// и инлайнинга, делегат предиката перестал инлайниться в TryGetFirst.
/// Регресс оставлен в LTS осознанно ("чинить в десятке - сломаем
/// другое") и перенесён в .NET 11.
///
/// Пруфы:
///   Регресс и разбор - issue dotnet/runtime #117717 (комментарии
///   AndyAyersMS с профилями: в net9 предиката нет отдельной строкой -
///   заинлайнен, в net10 <Solve>b__6 ест 21% сам по себе)
///       https://github.com/dotnet/runtime/issues/117717
///   Частичный фикс - PR #117816 (AndyAyersMS)
///       https://github.com/dotnet/runtime/pull/117816
///   Переоткрыто как не исправленное - issue #119425
///       https://github.com/dotnet/runtime/issues/119425
///
/// ВАЖНО про дизасм: регресс живёт в Tier1 с PGO. Снимать с
/// DOTNET_TieredCompilation=0 НЕЛЬЗЯ - без tiered-профиля эффект
/// исчезает. Для этого есть snap_pgo.bat: обычный tiered-прогрев,
/// в файле смотреть ПОСЛЕДНИЙ листинг метода (Tier1 with Dynamic PGO).
///
/// NoInlining на методах - только чтобы каждый печатался в листинге
/// отдельно и под своим именем.
/// </summary>
public sealed class Item
{
    public int X;
    public int Y;
}

public static class Subjects
{
    // ------------------------------------------------------------------
    // Свой цикл по List: индексатор, сравнение полей, выход по совпадению.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Item FirstFor(List<Item> items, int x, int y)
    {
        for (int i = 0; i < items.Count; i++)
        {
            Item item = items[i];
            if (item.X == x && item.Y == y)
            {
                return item;
            }
        }

        throw new InvalidOperationException("не найдено");
    }

    // ------------------------------------------------------------------
    // foreach по List - энумератор-структура, без LINQ.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Item FirstForeach(List<Item> items, int x, int y)
    {
        foreach (Item item in items)
        {
            if (item.X == x && item.Y == y)
            {
                return item;
            }
        }

        throw new InvalidOperationException("не найдено");
    }

    // ------------------------------------------------------------------
    // LINQ с замыканием - главный подозреваемый. Лямбда захватывает
    // x и y, компилятор делает display class и делегат. Именно этот
    // делегат в .NET 10 перестал инлайниться в TryGetFirst.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Item FirstLinq(List<Item> items, int x, int y) => items.First(item => item.X == x && item.Y == y);

    // ------------------------------------------------------------------
    // LINQ с кэшированным делегатом - захвата нет, делегат один на все
    // вызовы. Отделяет цену замыкания от цены самого делегатного вызова:
    // если регресс и тут - дело не в display class.
    // Цель поиска зашита константами TargetX/TargetY.
    // ------------------------------------------------------------------

    public const int TargetX = 1_000_003;
    public const int TargetY = 1_000_033;

    private static readonly Func<Item, bool> CachedPredicate = static item => item is { X: TargetX, Y: TargetY };

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Item FirstLinqCached(List<Item> items) => items.First(CachedPredicate);

    // ------------------------------------------------------------------
    // Генерализация: Count с предикатом - тот же делегатный путь,
    // полный проход по списку гарантирован самой семантикой.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int CountLinq(List<Item> items, int x) => items.Count(item => item.X == x);

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int CountFor(List<Item> items, int x)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].X == x)
            {
                count++;
            }
        }

        return count;
    }

    // ------------------------------------------------------------------
    // OSR-репро. Регресс #117717 завязан на OSR: длинный цикл внутри
    // ОДНОГО вызова большого метода (как Solve в исходном issue) -
    // метод компилируется через on-stack replacement, и именно в этой
    // связке PGO + инлайнинг делегат предиката теряет инлайн.
    // Обычный tiering (короткие горячие вызовы) регресс не ловит -
    // проверено: там предикат инлайнится и в 9, и в 10, и в 11.
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long OsrLinq(List<Item> items, int x, int y, int iterations)
    {
        long sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            sum += items.First(item => item.X == x && item.Y == y).X;
        }

        return sum;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long OsrFor(List<Item> items, int x, int y, int iterations)
    {
        long sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < items.Count; j++)
            {
                Item item = items[j];
                if (item.X == x && item.Y == y)
                {
                    sum += item.X;
                    break;
                }
            }
        }

        return sum;
    }

    // ------------------------------------------------------------------
    // Контроль из прошлой статьи: Sum() без делегата по List<int>.
    // Span-путь, векторный - регресс его не трогает. Показывает, что
    // просадка точечная, а не "LINQ в десятке сломали".
    // ------------------------------------------------------------------

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int SumControl(List<int> numbers) => numbers.Sum();
}
