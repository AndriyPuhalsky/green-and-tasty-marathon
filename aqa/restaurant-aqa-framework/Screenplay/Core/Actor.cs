using System.Collections.Concurrent;

namespace RestaurantAqa.Screenplay.Core;

public sealed class Actor(string name) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<Type, IAbility> _abilities = new();
    private readonly ConcurrentDictionary<string, object> _memory = new();

    public string Name { get; } = name;

    public Actor WhoCan(params IAbility[] abilities)
    {
        foreach (var ability in abilities)
        {
            _abilities[ability.GetType()] = ability;
        }
        return this;
    }

    public bool Has<TAbility>() where TAbility : IAbility => _abilities.ContainsKey(typeof(TAbility));

    public TAbility AbilityTo<TAbility>() where TAbility : IAbility
        => _abilities.TryGetValue(typeof(TAbility), out var ability)
            ? (TAbility)ability
            : throw new InvalidOperationException($"{Name} does not have the ability {typeof(TAbility).Name}.");

    public async Task AttemptsTo(params ITask[] tasks)
    {
        foreach (var task in tasks)
        {
            await task.PerformAs(this);
        }
    }

    public Task<TAnswer> AsksFor<TAnswer>(IQuestion<TAnswer> question) => question.AnsweredBy(this);

    public void Remember<T>(string key, T value) where T : notnull => _memory[key] = value;

    public T Recall<T>(string key)
        => _memory.TryGetValue(key, out var value)
            ? (T)value
            : throw new InvalidOperationException($"{Name} does not remember '{key}'.");

    public async ValueTask DisposeAsync()
    {
        foreach (var ability in _abilities.Values.OfType<IDisposableAbility>())
        {
            await ability.DisposeAsync();
        }
        _abilities.Clear();
    }

    public override string ToString() => Name;
}
