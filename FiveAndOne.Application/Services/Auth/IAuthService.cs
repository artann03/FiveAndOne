using FiveAndOne.Application.Dtos.Auth;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RegisterBusinessAsync(FiveAndOne.Application.Dtos.Businesses.RegisterBusinessRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AuthResponse> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    Task DeactivateAccountAsync(Guid userId, DeactivateAccountRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> ReactivateAccountAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
