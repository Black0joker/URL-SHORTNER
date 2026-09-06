namespace URLShortener.Domain.Exceptions;

public class UrlNotFoundException : DomainException
{
    public UrlNotFoundException(string message = "URL not found.") : base(message) { }
}
