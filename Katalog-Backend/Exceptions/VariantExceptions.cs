namespace Katalog_Backend.Exceptions;

public class VariantNotFoundException(int id) 
    : AppException($"Variant with ID {id} was not found.");

public class VariantImageNotFoundException(int imageId) 
    : AppException($"Variant image with ID {imageId} was not found.");

public class DuplicateSkuException(string sku) 
    : AppException($"A variant with SKU '{sku}' already exists.");

public class InvalidVariantOperationException(string message) 
    : AppException(message);
