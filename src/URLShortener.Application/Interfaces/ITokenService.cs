using URLShortener.Domain.Entities;

namespace URLShortener.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
