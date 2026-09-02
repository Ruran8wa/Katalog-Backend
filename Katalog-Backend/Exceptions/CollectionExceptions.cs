namespace Katalog_Backend.Exceptions;

public class CollectionNotFoundException(int id)
    : AppException($"Collection with ID {id} was not found.");

public class DuplicateCollectionNameException(string name)
    : AppException($"A collection with the name '{name}' already exists.");

public class ProductAlreadyInCollectionException(int productId, int collectionId)
    : AppException($"Product with ID {productId} is already in collection {collectionId}.");

public class ProductNotInCollectionException(int productId, int collectionId)
    : AppException($"Product with ID {productId} is not in collection {collectionId}.");
