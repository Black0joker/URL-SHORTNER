namespace URLShortener.Domain.Entities;

public class Url
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string ShortCode { get; set; }
    public required string OriginalUrl { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
    public string? CreatedByIp { get; set; }

    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
}
