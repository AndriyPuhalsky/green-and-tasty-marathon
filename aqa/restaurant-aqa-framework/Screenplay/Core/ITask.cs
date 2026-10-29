namespace RestaurantAqa.Screenplay.Core;

/// <summary>Дія актора на рівні бізнес-цілі ("приєднатися до групи", "підтвердити pre-order").</summary>
public interface ITask
{
    Task PerformAs(Actor actor);
}
