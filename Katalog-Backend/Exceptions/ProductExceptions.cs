namespace Katalog_Backend.Exceptions;

public class ProductNotFoundException(int id) 
    : AppException($"Product with ID {id} was not found.");
