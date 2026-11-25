using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Application.Dtos.Users;
using FiveAndOne.Domain.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Users;

public interface IUserService
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<User> CreateUserAsync(
        string firstName,
        string? lastName,
        string username,
        string email,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default);

    Task<PagedResponse<UserResponse>> GetUsersAsync(int page, int pageSize, UserRole? role, CancellationToken cancellationToken = default);
    Task<UserResponse> ChangeRoleAsync(Guid actingUserId, Guid targetUserId, UserRole newRole, CancellationToken cancellationToken = default);
    Task EnsureSuperAdminAsync(string firstName, string username, string email, string password, CancellationToken cancellationToken = default);
}
