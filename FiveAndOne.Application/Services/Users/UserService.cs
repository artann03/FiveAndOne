using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Repositories.RefreshTokens;
using FiveAndOne.Infrastructure.Repositories.Users;
using Microsoft.AspNetCore.Identity;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Users;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher<User> _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher<User> passwordHasher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByIdAsync(id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByUsernameAsync(username, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _userRepository.EmailExistsAsync(NormalizeEmail(email), cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _userRepository.UsernameExistsAsync(username, cancellationToken);
    }

    public async Task<User> CreateUserAsync(
        string firstName,
        string? lastName,
        string username,
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default,
        string? phoneNumber = null,
        string? preferredLanguage = null)
    {
        var normalizedEmail = NormalizeEmail(email);
        var trimmedUsername = username.Trim();

        if (await _userRepository.EmailExistsAsync(normalizedEmail, cancellationToken))
        {
            throw new ConflictException("Email is already in use.");
        }

        if (await _userRepository.UsernameExistsAsync(trimmedUsername, cancellationToken))
        {
            throw new ConflictException("Username is already taken.");
        }

        var user = new User
        {
            FirstName = firstName.Trim(),
            LastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim(),
            Username = trimmedUsername,
            Email = normalizedEmail,
            Role = role,
            PhoneNumber = string.IsNullOrWhiteSpace(phoneNumber) ? null : phoneNumber.Trim(),
            PreferredLanguage = string.IsNullOrWhiteSpace(preferredLanguage) ? "sq" : preferredLanguage.Trim().ToLowerInvariant()
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, password);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<string> GenerateUsernameAsync(string email, CancellationToken cancellationToken = default)
    {
        var local = NormalizeEmail(email).Split('@')[0];
        var baseName = new string(local.Where(c => char.IsLetterOrDigit(c) || c is '_' or '.').ToArray());
        if (baseName.Length < 3) baseName = (baseName + "user").PadRight(3, '0');
        if (baseName.Length > 40) baseName = baseName[..40];

        var candidate = baseName;
        for (var i = 2; await _userRepository.UsernameExistsAsync(candidate, cancellationToken); i++)
        {
            candidate = $"{baseName}{i}";
        }

        return candidate;
    }

    public async Task<PagedResponse<UserResponse>> GetUsersAsync(
        int page,
        int pageSize,
        UserRole? role,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (items, totalCount) = await _userRepository.GetPagedAsync(page, pageSize, role, cancellationToken);

        return new PagedResponse<UserResponse>
        {
            Items = items.Select(UserResponse.FromUser).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    public async Task<UserResponse> ChangeRoleAsync(
        Guid actingUserId,
        Guid targetUserId,
        UserRole newRole,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(newRole))
        {
            throw new BadRequestException("Unknown role.");
        }

        // Also guarantees there is always at least one SuperAdmin left.
        if (actingUserId == targetUserId)
        {
            throw new BadRequestException("You cannot change your own role.");
        }

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken)
            ?? throw new NotFoundException("User not found.");

        if (user.Role == newRole)
        {
            return UserResponse.FromUser(user);
        }

        var now = DateTime.UtcNow;
        user.Role = newRole;
        user.UpdatedAt = now;

        // The role lives inside the user's tokens, so sign them out everywhere.
        // Their current access token keeps working until it expires (max 15 min), then they must log in again.
        var activeTokens = await _refreshTokenRepository.GetActiveByUserIdAsync(user.Id, cancellationToken);
        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        return UserResponse.FromUser(user);
    }

    public async Task EnsureSuperAdminAsync(
        string firstName,
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (await _userRepository.AnyWithRoleAsync(UserRole.SuperAdmin, cancellationToken))
        {
            return;
        }

        var existing = await _userRepository.GetByEmailAsync(NormalizeEmail(email), cancellationToken);

        if (existing is not null)
        {
            existing.Role = UserRole.SuperAdmin;
            existing.UpdatedAt = DateTime.UtcNow;
            await _userRepository.SaveChangesAsync(cancellationToken);
            return;
        }

        await CreateUserAsync(firstName, null, username, email, password, UserRole.SuperAdmin, cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
