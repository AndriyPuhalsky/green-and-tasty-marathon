namespace RestaurantAqa.Screenplay.Core;

/// <summary>Що актор уміє: викликати API, працювати в браузері тощо.</summary>
public interface IAbility;

/// <summary>Здатність, яку треба закрити після сценарію (браузер, HTTP-клієнт).</summary>
public interface IDisposableAbility : IAbility, IAsyncDisposable;
