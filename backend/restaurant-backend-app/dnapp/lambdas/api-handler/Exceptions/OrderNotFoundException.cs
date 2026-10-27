namespace APIHandler.Exceptions;

public class OrderNotFoundException(string message = "Order not found.") : ValidationException(message);