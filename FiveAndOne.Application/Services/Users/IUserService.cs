using System;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Users;

public interface IUserService
{
    Task<User> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);
    Task<User> CreateUserAsync(
        string firstName,
        string? lastName,
        string username,
        string email,
        string password,
        CancellationToken cancellationToken = default);
}
