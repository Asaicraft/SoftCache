using System.Text.Json.Serialization;
using SoftCache;
using SoftCache.Annotations;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

builder.Services.AddMemoryCache();

var app = builder.Build();

var sampleTodos = new Todo[] {
    new(1, "Walk the dog"),
    new(2, "Do the dishes", DateOnly.FromDateTime(DateTime.Now)),
    new(3, "Do the laundry", DateOnly.FromDateTime(DateTime.Now.AddDays(1))),
    new(4, "Clean the bathroom"),
    new(5, "Clean the car", DateOnly.FromDateTime(DateTime.Now.AddDays(2)))
};

var todosApi = app.MapGroup("/todos");
todosApi.MapGet("/", () => sampleTodos);
todosApi.MapGet("/{id}", (int id) =>
    sampleTodos.FirstOrDefault(a => a.Id == id) is { } todo
        ? Results.Ok(todo)
        : Results.NotFound());


app.Run();

public sealed record Todo(int Id, string? Title, DateOnly? DueBy = null, bool IsComplete = false);

[JsonSerializable(typeof(Todo[]))]
[JsonSerializable(typeof(TodoQueryKey))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }

[SoftCache(
    CacheBits = 15,
    Associativity = 2,
    HashKind = SoftHashKind.XorFold,
    Concurrency = SoftCacheConcurrency.Lock,
    GenerateGlobalSeed = false,
    EnableDebugMetrics = false)]
public sealed partial class TodoQueryKey
{
    public string? Query { get; }
    public int FromDayNumber { get; }
    public int ToDayNumber { get; }
    public bool IsComplete { get; }
    public int Page { get; }
    public int PageSize { get; }
    public float Priority { get; } 
    public double CompletionScore { get; }

    public TodoQueryKey(
        string? query,
        int fromDayNumber,
        int toDayNumber,
        bool isComplete,
        int page,
        int pageSize,
        float priority,
        double completionScore)
    {
        Query = query;
        FromDayNumber = fromDayNumber;
        ToDayNumber = toDayNumber;
        IsComplete = isComplete;
        Page = page;
        PageSize = pageSize;
        Priority = priority;
        CompletionScore = completionScore;
    }
}
