namespace RestaurantAqa.Screenplay.Core;

/// <summary>Питання про стан системи, на яке актор знаходить відповідь.</summary>
public interface IQuestion<TAnswer>
{
    Task<TAnswer> AnsweredBy(Actor actor);
}
