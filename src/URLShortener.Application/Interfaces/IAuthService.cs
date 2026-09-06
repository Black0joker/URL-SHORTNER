using URLShortener.Application.DTOs;

namespace URLShortener.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshAsync(RefreshRequest request);
    Task LogoutAsync(string refreshToken);
    Task<UserResponse> GetCurrentUserAsync(Guid userId);
}
