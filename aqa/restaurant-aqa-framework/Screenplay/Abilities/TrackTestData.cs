using System.Collections.Concurrent;
using RestaurantAqa.Screenplay.Core;

namespace RestaurantAqa.Screenplay.Abilities;

/// <summary>
/// Спільний для акторів сценарію список прибирання. Задачі, які щось створюють на стенді,
/// одразу додають сюди дію для видалення; після сценарію дії виконуються у зворотному порядку.
/// </summary>
public sealed class TrackTestData : IAbility
{
    private readonly ConcurrentStack<(string What, Func<Task> Undo)> _undo = new();

    public void Created(string what, Func<Task> undo) => _undo.Push((what, undo));

    public async Task<IReadOnlyList<string>> CleanUp()
    {
        var problems = new List<string>();
        while (_undo.TryPop(out var item))
        {
            try
            {
                await item.Undo();
            }
            catch (Exception exception)
            {
                problems.Add($"{item.What}: {exception.Message}");
            }
        }
        return problems;
    }
}
