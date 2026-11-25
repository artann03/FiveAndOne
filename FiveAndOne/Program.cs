using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using FiveAndOne.API.Errors;
using FiveAndOne.API.Middleware;
using FiveAndOne.API.Seeding;
using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Services.Auth;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Catalog;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Infrastructure.Repositories.Businesses;
using FiveAndOne.Infrastructure.Repositories.Catalog;
using FiveAndOne.Infrastructure.Repositories.Facilities;
using FiveAndOne.Application.Services.Users;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Repositories.RefreshTokens;
using FiveAndOne.Infrastructure.Repositories.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ---------- Framework ----------
builder.Services.AddControllers()
    // Enums go over the wire as names ("Business") instead of numbers (2).
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Don't leak raw parser messages (type names, byte positions) in 400 responses.
        options.AllowInputFormatterExceptionMessages = false;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        // Validation errors (400) use our ErrorResponse instead of ValidationProblemDetails.
        options.InvalidModelStateResponseFactory = ErrorResponseFactory.FromInvalidModelState;
        // Don't turn empty NotFound()/Forbid() results into ProblemDetails; UseStatusCodePages below handles them.
        options.SuppressMapClientErrors = true;
    });

// Same enum-as-string setting for bodies written outside MVC (error responses from middleware).
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Adds the "Authorize" button in Swagger UI so you can paste a JWT access token.
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Paste the accessToken from /api/auth/login (without the 'Bearer ' prefix)."
        };
        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            }] = Array.Empty<string>()
        });
        return Task.CompletedTask;
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Database ----------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- JWT settings (fail fast on bad config) ----------
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException($"Missing '{JwtSettings.SectionName}' configuration section.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be set and at least 32 bytes long.");
}

builder.Services.Configure<JwtSettings>(jwtSection);

// ---------- Repositories & services ----------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBusinessProfileRepository, BusinessProfileRepository>();
builder.Services.AddScoped<IBusinessProfileService, BusinessProfileService>();
builder.Services.AddScoped<IFacilityRepository, FacilityRepository>();
builder.Services.AddScoped<ICatalogRepository, CatalogRepository>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.Configure<FacilitySettings>(builder.Configuration.GetSection(FacilitySettings.SectionName));

// ---------- Authentication & authorization ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep claim names as they are in the token ("sub", "email", ...).
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = RoleNames.ClaimType
        };

        // 401 / 403 from the auth pipeline use the same ErrorResponse body (by default they are empty).
        options.Events = new JwtBearerEvents
        {
            // Make deactivation/suspension take effect immediately instead of when the access token expires.
            // One primary-key lookup per authenticated request.
            OnTokenValidated = async context =>
            {
                var sub = context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();

                var isActive = Guid.TryParse(sub, out var userId)
                    && await db.Users.AnyAsync(
                        u => u.Id == userId && u.Status == UserStatus.Active,
                        context.HttpContext.RequestAborted);

                if (!isActive)
                {
                    context.Fail("The account is no longer active.");
                }
            },
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";

                var message = context.AuthenticateFailure switch
                {
                    SecurityTokenExpiredException => "Your access token has expired. Use /api/auth/refresh to get a new one.",
                    { Message: "The account is no longer active." } => "This account is no longer active.",
                    not null => "The access token is invalid.",
                    null => "Authentication is required. Send a valid access token in the Authorization header."
                };

                await ErrorResponseFactory.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, message);
            },
            OnForbidden = context =>
                ErrorResponseFactory.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden)
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Secure by default: every endpoint requires a valid access token
    // unless it is explicitly marked [AllowAnonymous].
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

await app.SeedSuperAdminAsync();

// ---------- HTTP pipeline ----------
// 1) Exceptions anywhere below -> ErrorResponse (GlobalExceptionHandler).
app.UseExceptionHandler();

// 2) Error status codes with an empty body (unknown route 404, 405, 415, NotFound() ...) -> ErrorResponse.
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    if (response.HasStarted || response.ContentLength > 0 || !string.IsNullOrEmpty(response.ContentType))
    {
        return;
    }

    await ErrorResponseFactory.WriteAsync(context.HttpContext, response.StatusCode);
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();          // JSON spec:  /openapi/v1.json
    app.UseSwaggerUI(options =>                  // UI:         /swagger
    {
        options.SwaggerEndpoint("/openapi/v1.json", "FiveAndOne API v1");
        options.EnablePersistAuthorization();    // keep the pasted token after a page refresh
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
