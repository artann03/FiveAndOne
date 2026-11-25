using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Auth;
using FiveAndOne.Application.Services.Users;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Repositories.RefreshTokens;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Auth;

public class AuthService : IAuthService
{
    private const string InvalidCredentials = "Invalid email or password.";
    private const string InvalidRefreshToken = "Invalid or expired refresh token.";

    private readonly IUserService _userService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        IUserService userService,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        IOptions<JwtSettings> jwtSettings)
    {
        _userService = userService;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Role is not (UserRole.User or UserRole.Business))
        {
            throw new BadRequestException("You can only register as 'User' or 'Business'.");
        }

        var user = await _userService.CreateUserAsync(
            request.FirstName,
            request.LastName,
            request.Username,
            request.Email,
            request.Password,
            request.Role,
            cancellationToken);

        return await IssueTokensAsync(user, null, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByCredentialsAsync(request, cancellationToken);

        if (!user.IsActive)
        {
            // The password was correct, so it is safe to tell the real owner why they can't get in.
            throw InactiveAccountError(user);
        }

        return await IssueTokensAsync(user, null, cancellationToken);
    }

    public async Task<AuthResponse> ReactivateAccountAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await GetUserByCredentialsAsync(request, cancellationToken);

        if (!user.IsActive)
        {
            if (!user.CanBeReactivated(DateTime.UtcNow))
            {
                throw InactiveAccountError(user);
            }

            user.Status = UserStatus.Active;
            user.DeactivatedAt = null;
            user.UpdatedAt = DateTime.UtcNow;
        }

        // Already active: behaves like a normal login. Saved together with the new refresh token.
        return await IssueTokensAsync(user, null, cancellationToken);
    }

    public async Task<AuthResponse> ChangePasswordAsync(
        Guid userId,
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword)
            == PasswordVerificationResult.Failed)
        {
            throw FieldError("currentPassword", "Current password is incorrect.", "invalid_password");
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            throw FieldError("newPassword", "New password must be different from the current password.");
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        // Sign out every other device; this device gets a fresh token pair in the response.
        await RevokeAllAsync(user.Id, cancellationToken);
        return await IssueTokensAsync(user, null, cancellationToken);
    }

    public async Task DeactivateAccountAsync(
        Guid userId,
        DeactivateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await GetActiveUserAsync(userId, cancellationToken);

        if (_passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
            == PasswordVerificationResult.Failed)
        {
            throw FieldError("password", "Password is incorrect.", "invalid_password");
        }

        // Prevents locking the platform out of its owner account.
        if (user.Role == UserRole.SuperAdmin)
        {
            throw new ForbiddenException(
                "A SuperAdmin account can't be deleted. Have another SuperAdmin change your role first.",
                "superadmin_cannot_deactivate");
        }

        var now = DateTime.UtcNow;
        user.Status = UserStatus.Deactivated;
        user.DeactivatedAt = now;
        user.UpdatedAt = now;

        // Saves the user change too (same DbContext).
        await RevokeAllAsync(user.Id, cancellationToken);
    }

    private async Task<User> GetUserByCredentialsAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByEmailAsync(request.Email, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentials);

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Saved together with the next SaveChanges (same DbContext).
            user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
            user.UpdatedAt = DateTime.UtcNow;
        }

        return user;
    }

    private async Task<User> GetActiveUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _userService.GetByIdAsync(userId, cancellationToken);

        return user is { IsActive: true }
            ? user
            : throw new UnauthorizedException("User no longer exists.");
    }

    private static AppException InactiveAccountError(User user)
    {
        if (user.Status == UserStatus.Suspended)
        {
            return new ForbiddenException("This account has been suspended.", "account_suspended");
        }

        if (user.CanBeReactivated(DateTime.UtcNow))
        {
            var deadline = user.DeactivatedAt!.Value.AddDays(User.ReactivationWindowDays);
            return new ForbiddenException(
                $"This account was deleted. You can restore it until {deadline:yyyy-MM-dd} (UTC) with POST /api/auth/reactivate.",
                "account_deactivated");
        }

        return new ForbiddenException(
            $"This account was deleted more than {User.ReactivationWindowDays} days ago and can no longer be restored.",
            "account_closed");
    }

    private static RequestValidationException FieldError(string field, string message, string code = "validation_failed")
    {
        return new RequestValidationException(
            message,
            new Dictionary<string, string[]> { [field] = new[] { message } },
            code);
    }

    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(
            _tokenService.HashToken(refreshToken), cancellationToken);

        if (stored is null)
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (stored.ReplacedByTokenId.HasValue)
        {
            // This token was already rotated, so someone is replaying an old one.
            // Assume it was stolen and kill every session for this user.
            await RevokeAllAsync(stored.UserId, cancellationToken);
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        if (!stored.IsActive || !stored.User.IsActive)
        {
            throw new UnauthorizedException(InvalidRefreshToken);
        }

        return await IssueTokensAsync(stored.User, stored, cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var stored = await _refreshTokenRepository.GetByTokenHashAsync(
            _tokenService.HashToken(refreshToken), cancellationToken);

        // Idempotent: logging out with an unknown or already revoked token is not an error.
        if (stored is null || stored.IsRevoked)
        {
            return;
        }

        stored.RevokedAt = DateTime.UtcNow;
        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    public Task LogoutAllAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return RevokeAllAsync(userId, cancellationToken);
    }

    private async Task RevokeAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(userId, cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthResponse> IssueTokensAsync(
        User user,
        RefreshToken? tokenBeingRotated,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var (accessToken, accessTokenExpiresAt) = _tokenService.CreateAccessToken(user);
        var rawRefreshToken = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = _tokenService.HashToken(rawRefreshToken),
            ExpiresAt = now.AddDays(_jwtSettings.RefreshTokenDays)
        };

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);

        if (tokenBeingRotated is not null)
        {
            tokenBeingRotated.RevokedAt = now;
            tokenBeingRotated.ReplacedByTokenId = refreshToken.Id;
        }

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        return new AuthResponse
        {
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = rawRefreshToken,
            RefreshTokenExpiresAt = refreshToken.ExpiresAt,
            UserId = user.Id,
            Email = user.Email,
            Username = user.Username,
            Role = user.Role,
            FirstName = user.FirstName,
            LastName = user.LastName
        };
    }
}
