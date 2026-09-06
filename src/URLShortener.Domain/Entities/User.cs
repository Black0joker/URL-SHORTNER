using Microsoft.AspNetCore.Identity;

namespace URLShortener.Domain.Entities;

public class User : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<Url> Urls { get; set; } = new List<Url>();
}
