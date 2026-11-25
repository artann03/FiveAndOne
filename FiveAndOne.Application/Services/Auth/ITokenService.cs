using FiveAndOne.Domain.Entities.Identity;
using System;

namespace FiveAndOne.Application.Services.Auth;

public interface ITokenService
{
    (string Token, DateTime ExpiresAt) CreateAccessToken(User user);
    string GenerateRefreshToken();
    string HashToken(string token);
}
