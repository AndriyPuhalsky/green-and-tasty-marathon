namespace APIHandler.Exceptions;

public class CartIsEmptyException(string message = "The cart is empty") : ValidationException(message);