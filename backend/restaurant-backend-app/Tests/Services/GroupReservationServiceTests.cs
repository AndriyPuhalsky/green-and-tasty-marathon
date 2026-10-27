using APIHandler.Constants;
using APIHandler.Constants.Orders;
using APIHandler.Entities;
using APIHandler.Exceptions;
using APIHandler.Models.GroupReservation;
using APIHandler.Repositories.GroupReservations;
using APIHandler.Repositories.GuestSessions;
using APIHandler.Repositories.Orders;
using APIHandler.Repositories.Reservations;
using APIHandler.Repositories.Tables;
using APIHandler.Services.Auth;
using APIHandler.Services.GroupReservations;
using APIHandler.Services.Locations;
using AutoFixture.Xunit2;
using Moq;

namespace APIHandler.Tests.Services;

public class GroupReservationServiceTests
{
    private readonly Mock<IGroupReservationRepository> _groupReservationRepositoryMock = new();
    private readonly Mock<ITableRepository> _tableRepositoryMock = new();
    private readonly Mock<IReservationRepository> _reservationRepositoryMock = new();
    private readonly Mock<IGuestSessionRepository> _guestSessionRepositoryMock = new();
    private readonly Mock<IUserService> _userServiceMock = new();
    private readonly Mock<ILocationService> _locationServiceMock = new();
    private readonly Mock<IOrderRepository> _orderRepositoryMock = new();

    private readonly GroupReservationService _service;

    public GroupReservationServiceTests()
    {
        _service = new GroupReservationService(
            _groupReservationRepositoryMock.Object,
            _tableRepositoryMock.Object,
            _reservationRepositoryMock.Object,
            _guestSessionRepositoryMock.Object,
            _userServiceMock.Object,
            _locationServiceMock.Object,
            _orderRepositoryMock.Object);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_ValidInput_ReturnsGroupReservationInfo(
        GroupReservation groupReservation,
        Table table,
        Reservation reservation,
        Location location,
        string expectedUserName)
    {
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        reservation.TableId = table.Id;
        table.LocationId = location.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(0);

        _tableRepositoryMock
            .Setup(r => r.GetTableAsync(table.Id))
            .ReturnsAsync(table);

        _locationServiceMock
            .Setup(s => s.GetLocationByIdAsync(table.LocationId))
            .ReturnsAsync(location);

        _userServiceMock
            .Setup(s => s.GetUserName(reservation.CustomerId!))
            .ReturnsAsync(expectedUserName);

        var result = await _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken);

        Assert.Equal(groupReservation.GroupReservationId, result.GroupReservationId);
        Assert.Equal(table.LocationId, result.LocationId);
        Assert.Equal(reservation.GuestCount, result.MaxGuestCount);
        Assert.Equal(reservation.StartTime, result.StartTime);
        Assert.Equal(reservation.EndTime, result.EndTime);
        Assert.Equal(table.TableNumber, result.TableNumber);
        Assert.Equal("Green & Tasty", result.RestaurantName);
        Assert.Equal(location.Address, result.LocationAddress);
        Assert.Equal(expectedUserName, result.InviterName);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_InvalidToken_ThrowsNotFound(string token)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(token))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.GetGroupReservationByTokenAsync(token));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed)]
    [InlineAutoData(GroupReservationStatus.Submitted)]
    public async Task GetGroupReservationByTokenAsync_GroupReservationIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        GroupReservation groupReservation)
    {
        groupReservation.Status = status;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync<GroupReservationExpiredException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationByTokenAsync_WhenGuestCountIsMax_ThrowsGuestLimitExceeded(GroupReservation groupReservation, Reservation reservation)
    {
        groupReservation.Status = GroupReservationStatus.Open;
        groupReservation.ReservationId = reservation.Id;

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByTokenAsync(groupReservation.ReservationToken))
            .ReturnsAsync(groupReservation);

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _guestSessionRepositoryMock
            .Setup(r => r.CountByGroupReservationId(groupReservation.GroupReservationId))
            .ReturnsAsync(reservation.GuestCount);

        await Assert.ThrowsAsync<GuestLimitExceededException>(() => _service.GetGroupReservationByTokenAsync(groupReservation.ReservationToken));
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_ValidInput_UpdatesGroupReservationStatus(GroupReservation groupReservation)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(groupReservation.ReservationId))
            .ReturnsAsync(groupReservation);

        GroupReservation? savedGroupReservation = null;
        _groupReservationRepositoryMock
            .Setup(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()))
            .Callback<GroupReservation>(r => savedGroupReservation = r);

        await _service.CloseGroupReservationAsync(groupReservation.ReservationId);

        Assert.NotNull(savedGroupReservation);
        Assert.Equal(GroupReservationStatus.Closed, savedGroupReservation.Status);
    }

    [Theory]
    [AutoData]
    public async Task CloseGroupReservationAsync_GroupReservationNotFound_DoesNotThrowException(string reservationId)
    {
        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservationId))
            .ReturnsAsync((GroupReservation?)null);

        await _service.CloseGroupReservationAsync(reservationId);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_ValidInput_ReturnsToken(Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        GetGroupInviteTokenResponse result = await _service.GetGroupReservationInviteTokenAsync(reservation.Id);

        Assert.Equal(groupReservation.ReservationToken, result.Token);
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_InvalidReservationId_ThrowsNotFound(string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.GetGroupReservationInviteTokenAsync(reservationId));
    }

    [Theory]
    [AutoData]
    public async Task GetGroupReservationInviteTokenAsync_OneGuest_ThrowsValidationException(Reservation reservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(ReservationStatus.Completed)]
    [InlineAutoData(ReservationStatus.Cancelled)]
    [InlineAutoData(ReservationStatus.InProgress)]
    public async Task GetGroupReservationInviteTokenAsync_StatusIsNotReserved_ThrowsValidationException(ReservationStatus reservationStatus, Reservation reservation)
    {
        reservation.Status = reservationStatus;
        reservation.GuestCount = 1;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        await Assert.ThrowsAsync<ValidationException>(() => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    public async Task GetGroupReservationInviteTokenAsync_GroupStatusIsNotOpen_ThrowsException(GroupReservationStatus status, Type type, Reservation reservation, GroupReservation groupReservation)
    {
        reservation.Status = ReservationStatus.Reserved;
        reservation.GuestCount = 3;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = status;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetByReservationIdAsync(reservation.Id))
            .ReturnsAsync(groupReservation);

        await Assert.ThrowsAsync(type, () => _service.GetGroupReservationInviteTokenAsync(reservation.Id));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_ValidInput_MergesSubmittedDishesByDishId(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);
        order.Dishes = [];

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(
            [
                CreateSession(groupReservation.GroupReservationId, true, ("dish1", 2), ("dish2", 1)),
                CreateSession(groupReservation.GroupReservationId, true, ("dish1", 1), ("dish2", 1)),
                CreateSession(groupReservation.GroupReservationId, false, ("dish1", 5), ("dish3", 1))
            ]);

        var result = (await _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id)).ToList();

        Assert.Equal(new[] { new GroupPreOrderItemResponse("dish1", 3), new GroupPreOrderItemResponse("dish2", 2) }, result);

        Assert.Equal(2, order.Dishes.Count);
        Assert.Equal(3, order.Dishes.Single(d => d.DishId == "dish1").Quantity);
        Assert.Equal(2, order.Dishes.Single(d => d.DishId == "dish2").Quantity);
        Assert.DoesNotContain(order.Dishes, d => d.DishId == "dish3");
        Assert.All(order.Dishes, d => Assert.Equal(OrderItemState.Pending, d.State));
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_ValidInput_UpdatesOrderAndGroupReservationStatuses(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([CreateSession(groupReservation.GroupReservationId, true, ("dish1", 1))]);

        await _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id);

        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.Is<Order>(o =>
            o.OrderId == order.OrderId && o.Status == OrderStatus.SubmittedByCustomer)), Times.Once);

        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.Is<GroupReservation>(g =>
            g.GroupReservationId == groupReservation.GroupReservationId && g.Status == GroupReservationStatus.Submitted)), Times.Once);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_OrderAlreadyHasSameDish_SumsQuantities(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);
        order.Dishes =
        [
            new OrderItem { DishId = "dish1", Quantity = 1, State = OrderItemState.Confirmed },
            new OrderItem { DishId = "dish4", Quantity = 2, State = OrderItemState.Confirmed }
        ];

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(
            [
                CreateSession(groupReservation.GroupReservationId, true, ("dish1", 2)),
                CreateSession(groupReservation.GroupReservationId, true, ("dish1", 1), ("dish2", 1))
            ]);

        await _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id);

        Assert.Equal(3, order.Dishes.Count);

        var mergedDish = Assert.Single(order.Dishes, d => d.DishId == "dish1");
        Assert.Equal(4, mergedDish.Quantity);
        Assert.Equal(OrderItemState.Pending, mergedDish.State);

        var untouchedDish = Assert.Single(order.Dishes, d => d.DishId == "dish4");
        Assert.Equal(2, untouchedDish.Quantity);
        Assert.Equal(OrderItemState.Confirmed, untouchedDish.State);

        Assert.Equal(1, Assert.Single(order.Dishes, d => d.DishId == "dish2").Quantity);
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_ReservationNotFound_ThrowsReservationNotFoundException(string userId, string groupReservationId, string reservationId)
    {
        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservationId))
            .ReturnsAsync((Reservation?)null);

        await Assert.ThrowsAsync<ReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservationId));

        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_GroupReservationNotFound_ThrowsGroupReservationNotFoundException(Reservation reservation, string userId, string groupReservationId)
    {
        reservation.CustomerId = userId;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservationId))
            .ReturnsAsync((GroupReservation?)null);

        await Assert.ThrowsAsync<GroupReservationNotFoundException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_CallerIsNotOwner_ThrowsActionForbiddenException(Reservation reservation, GroupReservation groupReservation, Order order, string userId, string anotherUserId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);

        await Assert.ThrowsAsync<ActionForbiddenException>(() => _service.SubmitGroupPreOrderAsync(anotherUserId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_GroupBelongsToAnotherReservation_ThrowsValidationException(Reservation reservation, GroupReservation groupReservation, Order order, string userId, string anotherReservationId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);
        groupReservation.ReservationId = anotherReservationId;

        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [InlineAutoData(GroupReservationStatus.Submitted, typeof(GroupReservationSubmittedException))]
    [InlineAutoData(GroupReservationStatus.Closed, typeof(GroupReservationExpiredException))]
    public async Task SubmitGroupPreOrderAsync_GroupStatusIsNotOpen_ThrowsException(
        GroupReservationStatus status,
        Type exceptionType,
        Reservation reservation,
        GroupReservation groupReservation,
        Order order,
        string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);
        groupReservation.Status = status;

        await Assert.ThrowsAsync(exceptionType, () => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [InlineAutoData(0)]
    [InlineAutoData(2)]
    public async Task SubmitGroupPreOrderAsync_NoSubmittedSessions_ThrowsGuestSessionsNotSubmittedException(
        int notSubmittedSessionsCount,
        Reservation reservation,
        GroupReservation groupReservation,
        Order order,
        string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);

        var sessions = Enumerable.Range(0, notSubmittedSessionsCount)
            .Select(_ => CreateSession(groupReservation.GroupReservationId, false, ("dish1", 1)))
            .ToList();

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(sessions);

        await Assert.ThrowsAsync<GuestSessionsNotSubmittedException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_SubmittedSessionsHaveNoDishes_ThrowsCartIsEmptyException(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(
            [
                CreateSession(groupReservation.GroupReservationId, true),
                CreateSession(groupReservation.GroupReservationId, false, ("dish1", 1))
            ]);

        await Assert.ThrowsAsync<CartIsEmptyException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    [Theory]
    [AutoData]
    public async Task SubmitGroupPreOrderAsync_OrderNotFound_ThrowsOrderNotFoundException(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        SetupGroupPreOrder(reservation, groupReservation, order, userId);

        _guestSessionRepositoryMock
            .Setup(r => r.GetAllByGroupReservationIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync([CreateSession(groupReservation.GroupReservationId, true, ("dish1", 1))]);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync((Order?)null);

        await Assert.ThrowsAsync<OrderNotFoundException>(() => _service.SubmitGroupPreOrderAsync(userId, groupReservation.GroupReservationId, reservation.Id));

        VerifyNothingSaved();
    }

    private void SetupGroupPreOrder(Reservation reservation, GroupReservation groupReservation, Order order, string userId)
    {
        reservation.CustomerId = userId;
        groupReservation.ReservationId = reservation.Id;
        groupReservation.Status = GroupReservationStatus.Open;
        order.ReservationId = reservation.Id;
        order.Status = OrderStatus.Created;

        _reservationRepositoryMock
            .Setup(r => r.GetReservationByIdAsync(reservation.Id))
            .ReturnsAsync(reservation);

        _groupReservationRepositoryMock
            .Setup(r => r.GetGroupReservationByIdAsync(groupReservation.GroupReservationId))
            .ReturnsAsync(groupReservation);

        _orderRepositoryMock
            .Setup(r => r.GetOrderByReservationIdAsync(reservation.Id))
            .ReturnsAsync(order);
    }

    private void VerifyNothingSaved()
    {
        _orderRepositoryMock.Verify(r => r.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
        _groupReservationRepositoryMock.Verify(r => r.SaveGroupReservationAsync(It.IsAny<GroupReservation>()), Times.Never);
    }

    private static GuestSession CreateSession(string groupReservationId, bool isSubmitted, params (string DishId, int Quantity)[] dishes) => new()
    {
        Id = Guid.NewGuid().ToString(),
        GroupReservationId = groupReservationId,
        GuestName = "Guest",
        IsSubmitted = isSubmitted,
        Dishes = dishes.Select(d => new OrderItem { DishId = d.DishId, Quantity = d.Quantity }).ToList()
    };
}
