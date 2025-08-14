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

    // Minimal Parameters for the benchmark (mirrors the generator output)
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

    // Your MakeSoftHash implementation — inlined here for a self-contained benchmark
    public static unsafe ushort MakeSoftHash(scoped in Parameters parameters)
    {
        uint h = 0u;
        unchecked
        {
            object? object0 = (object?)parameters.Query;
            uint x0 = object0 is null ? 0u : (uint)RuntimeHelpers.GetHashCode(object0);
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
[SimpleJob]
public class HashBench
{
    // Sample size used for timing and collision measurements
    [Params(10_000, 100_000, 500_000)]
    public int SampleSize;

    private TodoQueryKey.Parameters[] _data = default!;

    [GlobalSetup]
    public void Setup()
    {
        // Deterministic dataset
        var random = new Random(12345);
        var vocabulary = CreateVocabulary(random, 2048);

        _data = new TodoQueryKey.Parameters[SampleSize];
        for (var i = 0; i < SampleSize; i++)
        {
            // Add some variability to produce realistic collisions
            var queryText = (i % 7 == 0) ? null : vocabulary[random.Next(vocabulary.Length)];
            var fromDayNumber = random.Next(73_000, 85_000); // DateOnly.DayNumber ~ 2000..2030
            var toDayNumber = fromDayNumber + random.Next(0, 30);
            var isComplete = (i & 1) == 0;
            var page = 1 + random.Next(0, 128);
            var pageSize = 1 + random.Next(1, 128);

            _data[i] = new TodoQueryKey.Parameters(queryText, fromDayNumber, toDayNumber, isComplete, page, pageSize);
        }
    }

    // ---------- TIME ----------

    [Benchmark(Baseline = true)]
    public ulong HashCodeCombine_Time()
    {
        // Accumulate hashes so JIT cannot eliminate the computation
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            // Fair baseline: use standard HashCode.Combine and truncate to 16-bit
            var h32 = HashCode.Combine(parameters.Query, parameters.FromDayNumber, parameters.ToDayNumber, parameters.IsComplete, parameters.Page, parameters.PageSize);
            sum += (ushort)h32;
        }

        return sum;
    }

    [Benchmark]
    public ulong SoftHash_Time()
    {
        ulong sum = 0;
        var data = _data;

        for (var i = 0; i < data.Length; i++)
        {
            ref readonly var parameters = ref data[i];
            sum += TodoQueryKey.MakeSoftHash(in parameters);
        }

        return sum;
    }

    private static string[] CreateVocabulary(Random random, int size)
    {
        var array = new string[size];
        for (var i = 0; i < size; i++)
        {
            // Short keywords of varying length — emulate search queries
            var length = 3 + random.Next(0, 10);
            var characters = new char[length];
            for (var j = 0; j < length; j++)
            {
                // a-z letters
                characters[j] = (char)('a' + random.Next(0, 26));
            }
            array[i] = new string(characters);
        }
        return array;
    }
}
