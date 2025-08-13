using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

public sealed partial class TodoQueryKey
{
    public string? Query { get; }
    public int FromDayNumber { get; }
    public int ToDayNumber { get; }
    public bool IsComplete { get; }
    public int Page { get; }
    public int PageSize { get; }

    public TodoQueryKey(string? query, int fromDayNumber, int toDayNumber, bool isComplete, int page, int pageSize)
    {
        Query = query;
        FromDayNumber = fromDayNumber;
        ToDayNumber = toDayNumber;
        IsComplete = isComplete;
        Page = page;
        PageSize = pageSize;
    }

    // Минимальный Parameters для бенча (как у твоего генератора)
    public readonly struct Parameters
    {
        public readonly string? Query { get; }
        public readonly int FromDayNumber { get; }
        public readonly int ToDayNumber { get; }
        public readonly bool IsComplete { get; }
        public readonly int Page { get; }
        public readonly int PageSize { get; }

        public Parameters(string? query, int fromDayNumber, int toDayNumber, bool isComplete, int page, int pageSize)
        {
            Query = query;
            FromDayNumber = fromDayNumber;
            ToDayNumber = toDayNumber;
            IsComplete = isComplete;
            Page = page;
            PageSize = pageSize;
        }
    }

    // Твоя реализация MakeSoftHash — скопировал сюда для самодостаточности бенча
    public static unsafe ushort MakeSoftHash(scoped in Parameters parameters)
    {
        uint h = 0u;
        unchecked
        {
            object? o0 = (object?)parameters.Query;
            uint x0 = o0 is null ? 0u : (uint)RuntimeHelpers.GetHashCode(o0);
            x0 ^= x0 >> 16;
            h ^= x0;

            uint x1 = (uint)parameters.FromDayNumber;
            x1 ^= x1 >> 16;
            h ^= x1;

            uint x2 = (uint)parameters.ToDayNumber;
            x2 ^= x2 >> 16;
            h ^= x2;

            uint x3 = (uint)(parameters.IsComplete ? 1 : 0);
            x3 ^= x3 >> 16;
            h ^= x3;

            uint x4 = (uint)parameters.Page;
            x4 ^= x4 >> 16;
            h ^= x4;

            uint x5 = (uint)parameters.PageSize;
            x5 ^= x5 >> 16;
            h ^= x5;
        }

        return (ushort)h;
    }
}

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class HashBench
{
    // Размер выборки для замеров/коллизий
    [Params(10_000, 100_000, 500_000)]
    public int N;

    private TodoQueryKey.Parameters[] _data = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Детерминированный набор
        var rng = new Random(12345);
        var vocab = CreateVocabulary(rng, 2048);

        _data = new TodoQueryKey.Parameters[N];
        for (int i = 0; i < N; i++)
        {
            // немного вариативности, чтобы получить реальные коллизии
            var q = (i % 7 == 0) ? null : vocab[rng.Next(vocab.Length)];
            var from = rng.Next(73_000, 85_000);      // DateOnly.DayNumber ~ 2000..2030
            var to = from + rng.Next(0, 30);
            var isComplete = (i & 1) == 0;
            var page = 1 + rng.Next(0, 128);
            var pageSize = 1 + rng.Next(1, 128);

            _data[i] = new TodoQueryKey.Parameters(q, from, to, isComplete, page, pageSize);
        }
    }

    // ---------- ВРЕМЯ ----------

    [Benchmark(Baseline = true)]
    public ulong HashCodeCombine_Time()
    {
        // суммируем хеши, чтобы JIT не выкинул вычисления
        ulong sum = 0;
        var data = _data;

        for (int i = 0; i < data.Length; i++)
        {
            ref readonly var p = ref data[i];
            // fair: берём стандартный HashCode.Combine и режем до 16-bit
            var h32 = HashCode.Combine(p.Query, p.FromDayNumber, p.ToDayNumber, p.IsComplete, p.Page, p.PageSize);
            sum += (ushort)h32;
        }

        return sum;
    }

    [Benchmark]
    public ulong SoftHash_Time()
    {
        ulong sum = 0;
        var data = _data;

        for (int i = 0; i < data.Length; i++)
        {
            ref readonly var p = ref data[i];
            sum += TodoQueryKey.MakeSoftHash(in p);
        }

        return sum;
    }

    // ---------- КОЛЛИЗИИ ----------

    [Benchmark]
    public int HashCodeCombine_Collisions()
    {
        var set = new HashSet<ushort>(capacity: _data.Length * 2);
        var data = _data;

        for (int i = 0; i < data.Length; i++)
        {
            ref readonly var p = ref data[i];
            var h32 = HashCode.Combine(p.Query, p.FromDayNumber, p.ToDayNumber, p.IsComplete, p.Page, p.PageSize);
            var h16 = (ushort)h32;
            set.Add(h16);
        }

        return data.Length - set.Count; // сколько «село» в один и тот же 16-бит слот
    }

    [Benchmark]
    public int SoftHash_Collisions()
    {
        var set = new HashSet<ushort>(capacity: _data.Length * 2);
        var data = _data;

        for (int i = 0; i < data.Length; i++)
        {
            ref readonly var p = ref data[i];
            var h16 = TodoQueryKey.MakeSoftHash(in p);
            set.Add(h16);
        }

        return data.Length - set.Count;
    }

    // ---------- helpers ----------

    private static string[] CreateVocabulary(Random rng, int size)
    {
        var arr = new string[size];
        for (int i = 0; i < size; i++)
        {
            // короткие ключевые слова разной длины — имитируют запросы
            var len = 3 + rng.Next(0, 10);
            var chars = new char[len];
            for (int j = 0; j < len; j++)
            {
                // простая a-z
                chars[j] = (char)('a' + rng.Next(0, 26));
            }
            arr[i] = new string(chars);
        }
        return arr;
    }
}
