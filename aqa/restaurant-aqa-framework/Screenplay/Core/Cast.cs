using System.Collections.Concurrent;

namespace RestaurantAqa.Screenplay.Core;

/// <summary>Актори одного сценарію. Кожен сценарій має свій Cast, тож паралельні сценарії не перетинаються.</summary>
public sealed class Cast : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Actor> _actors = new(StringComparer.OrdinalIgnoreCase);

    public Actor ActorNamed(string name, Func<Actor, Actor>? prepare = null)
        => _actors.GetOrAdd(name, n => prepare?.Invoke(new Actor(n)) ?? new Actor(n));

    public bool TryGet(string name, out Actor actor) => _actors.TryGetValue(name, out actor!);

    public IReadOnlyCollection<Actor> Actors => _actors.Values.ToArray();

    public async ValueTask DisposeAsync()
    {
        foreach (var actor in _actors.Values)
        {
            await actor.DisposeAsync();
        }
        _actors.Clear();
    }
}
