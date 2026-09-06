namespace URLShortener.Domain.Exceptions;

public class InvalidUrlException : DomainException
{
    public InvalidUrlException(string message) : base(message) { }
}
