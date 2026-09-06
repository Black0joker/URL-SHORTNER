namespace URLShortener.Domain.Exceptions;

public class ShortCodeConflictException : DomainException
{
    public string ShortCode { get; }
    public ShortCodeConflictException(string shortCode)
        : base($"The short code '{shortCode}' is already in use.")
    {
        ShortCode = shortCode;
    }
}
