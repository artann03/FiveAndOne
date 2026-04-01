using FiveAndOne.Application.Dtos.Auth;
using System.Threading;
using System.Threading.Tasks;

namespace FiveAndOne.Application.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
