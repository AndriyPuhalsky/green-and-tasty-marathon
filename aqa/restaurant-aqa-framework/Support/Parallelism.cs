using RestaurantAqa.Support;
using TUnit.Core.Interfaces;

[assembly: ParallelLimiter<StandParallelLimit>]

namespace RestaurantAqa.Support;

/// <summary>
/// Скільки сценаріїв іде одночасно. Lambda на стенді має ліміт одночасних викликів,
/// і на 10+ паралельних запитах API Gateway починає віддавати 500 (див. README), тому за замовчуванням 4.
/// </summary>
public sealed class StandParallelLimit : IParallelLimit
{
    public int Limit => TestSettings.Current.Parallelism;
}
