namespace Katalog_Backend.Exceptions;

public abstract class AppException(string message, Exception? innerException = null) 
    : Exception(message, innerException);

public class InvalidCredentialsException(string message = "Invalid email or password") 
    : AppException(message);

public class AccountLockedException(string message = "Account is locked due to multiple failed login attempts. Please try again later.") 
    : AppException(message);

public class RegistrationException : AppException
{
    public IEnumerable<string> Errors { get; }

    public RegistrationException(string message, IEnumerable<string>? errors = null) 
        : base(message)
    {
        Errors = errors ?? [];
    }

    public RegistrationException(IEnumerable<string> errors) 
        : base(string.Join("; ", errors))
    {
        Errors = errors;
    }
}
