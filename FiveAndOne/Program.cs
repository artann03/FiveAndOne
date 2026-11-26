using FiveAndOne.API.Errors;
using FiveAndOne.API.Infrastructure;
using FiveAndOne.API.Middleware;
using FiveAndOne.API.Providers;
using FiveAndOne.API.Seeding;
using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Auth;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Services.Accounts;
using FiveAndOne.Application.Services.Admin;
using FiveAndOne.Application.Services.Auth;
using FiveAndOne.Application.Services.Availability;
using FiveAndOne.Application.Services.Bookings;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Catalog;
using FiveAndOne.Application.Services.Community;
using FiveAndOne.Application.Services.Dashboard;
using FiveAndOne.Application.Services.Discovery;
using FiveAndOne.Application.Services.Facilities;
using FiveAndOne.Application.Services.Payments;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Application.Services.Subscriptions;
using FiveAndOne.Application.Services.Users;
using FiveAndOne.Domain.Entities.Identity;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Repositories.RefreshTokens;
using FiveAndOne.Infrastructure.Repositories.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ---------- Framework ----------
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums go over the wire as names ("Business") instead of numbers (2).
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        // Times as "HH:mm" (opening hours, slot times).
        options.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
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

// Same JSON settings for bodies written outside MVC (error responses from middleware).
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
});

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "SportX / FiveAndOne API";
        document.Info.Description =
            "Business endpoints (/api/business/...) act on the caller's business. If the user manages more than one, " +
            "send the X-Business-Id header (ids from GET /api/business/mine). Send Accept-Language (sq, en, tr, sr) for " +
            "translated catalogue names, Idempotency-Key when creating reservations and X-Device-Id for guest bookings.";

        // Adds the "Authorize" button in Swagger UI so you can paste a JWT access token.
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
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

// ---------- Database ----------
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Settings (fail fast on bad JWT config) ----------
var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
var jwtSettings = jwtSection.Get<JwtSettings>()
    ?? throw new InvalidOperationException($"Missing '{JwtSettings.SectionName}' configuration section.");

if (string.IsNullOrWhiteSpace(jwtSettings.Key) || Encoding.UTF8.GetByteCount(jwtSettings.Key) < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key must be set and at least 32 bytes long. In development run: dotnet user-secrets set \"Jwt:Key\" \"<random 64 chars>\" --project FiveAndOne");
}

builder.Services.Configure<JwtSettings>(jwtSection);
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection(AppSettings.SectionName));
builder.Services.Configure<FacilitySettings>(builder.Configuration.GetSection(FacilitySettings.SectionName));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection(PaymentSettings.SectionName));

// ---------- External providers (MOCKS: swap for real implementations, see docs/OPEN_QUESTIONS.md) ----------
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();
builder.Services.AddSingleton<ISmsSender, LoggingSmsSender>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddSingleton<IGeocodingService, MockGeocodingService>();
builder.Services.AddSingleton<IPaymentGateway, MockPaymentGateway>();
builder.Services.AddScoped<IRequestContext, HttpRequestContext>();

// ---------- Identity ----------
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();

// ---------- Platform ----------
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IFeatureFlags, FeatureFlagService>();
builder.Services.AddScoped<IPlatformSettings, PlatformSettingsService>();
builder.Services.AddScoped<ITranslationService, TranslationService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IAdminPlatformService, AdminPlatformService>();

// ---------- Businesses & facilities ----------
builder.Services.AddScoped<IBusinessAccess, BusinessAccess>();
builder.Services.AddScoped<IBusinessService, BusinessService>();
builder.Services.AddScoped<IStaffService, StaffService>();
builder.Services.AddScoped<IVenueService, VenueService>();
builder.Services.AddScoped<IFacilityService, FacilityService>();
builder.Services.AddScoped<IFacilityConfigService, FacilityConfigService>();
builder.Services.AddScoped<IFacilityImageService, FacilityImageService>();
builder.Services.AddScoped<ICatalogService, CatalogService>();
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
builder.Services.AddScoped<IDiscoveryService, DiscoveryService>();
builder.Services.AddScoped<IBusinessDashboardService, BusinessDashboardService>();

// ---------- Reservations, payments, subscriptions ----------
builder.Services.AddScoped<ReservationSupport>();
builder.Services.AddScoped<IBookingNotifier, BookingNotifier>();
builder.Services.AddScoped<IBookingLifecycle, BookingLifecycle>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<IBusinessReservationService, BusinessReservationService>();
builder.Services.AddScoped<IReservationPaymentService, ReservationPaymentService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();

// ---------- Community (post-MVP, behind feature flags) ----------
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IPlayerService, PlayerService>();
builder.Services.AddScoped<ITeamService, TeamService>();
builder.Services.AddScoped<IPhoneVerificationService, PhoneVerificationService>();
builder.Services.AddScoped<IPromotionService, PromotionService>();

// ---------- Background workers ----------
builder.Services.AddHostedService<NotificationWorker>();
builder.Services.AddHostedService<ReservationExpiryWorker>();
builder.Services.AddHostedService<SubscriptionWorker>();

// ---------- CORS (frontend origins from config "Cors:AllowedOrigins") ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:5173", "http://localhost:3000" };
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("WWW-Authenticate")));

// ---------- Rate limiting (stories 03, 32, 52): per IP, stricter for auth and anonymous booking ----------
// Limits per minute come from "RateLimiting" config (raise them behind a shared office IP or for test runs).
var limits = builder.Configuration.GetSection("RateLimiting");
var authPerMinute = limits.GetValue("AuthPerMinute", 20);
var guestBookingsPerMinute = limits.GetValue("GuestBookingsPerMinute", 10);
var verificationPer10Minutes = limits.GetValue("VerificationPer10Minutes", 10);
var requestsPerMinute = limits.GetValue("RequestsPerMinute", 600);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, cancellationToken) => new ValueTask(ErrorResponseFactory.WriteAsync(
        context.HttpContext, StatusCodes.Status429TooManyRequests, "Too many requests. Please slow down and try again shortly.", "rate_limited"));

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
    {
        var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = http.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(http.Request.Method))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"auth:{ip}", _ => new FixedWindowRateLimiterOptions { PermitLimit = authPerMinute, Window = TimeSpan.FromMinutes(1) });
        }

        if (path.StartsWith("/api/reservations", StringComparison.OrdinalIgnoreCase) && HttpMethods.IsPost(http.Request.Method)
            && http.Request.Headers.Authorization.Count == 0)
        {
            return RateLimitPartition.GetFixedWindowLimiter($"guest:{ip}", _ => new FixedWindowRateLimiterOptions { PermitLimit = guestBookingsPerMinute, Window = TimeSpan.FromMinutes(1) });
        }

        if (path.StartsWith("/api/verification", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetFixedWindowLimiter($"verify:{ip}", _ => new FixedWindowRateLimiterOptions { PermitLimit = verificationPer10Minutes, Window = TimeSpan.FromMinutes(10) });
        }

        return RateLimitPartition.GetFixedWindowLimiter($"all:{ip}", _ => new FixedWindowRateLimiterOptions { PermitLimit = requestsPerMinute, Window = TimeSpan.FromMinutes(1) });
    });
});

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
await app.SeedDevelopmentDataAsync();

// ---------- HTTP pipeline ----------
// 1) Exceptions anywhere below -> ErrorResponse (GlobalExceptionHandler).
//    The framework's own "fail" log for handled exceptions is turned off in appsettings
//    (Microsoft.AspNetCore.Diagnostics.ExceptionHandlerMiddleware); GlobalExceptionHandler logs real 500s itself.
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

// 3) Basic security headers (story 52).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
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
else
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// Uploaded facility images (local mock storage) under /uploads.
app.UseStaticFiles();

app.UseCors();
app.UseRateLimiter();

// Unknown /api routes are 404 for everyone. Without this the secure-by-default policy answers 401 to
// anonymous callers, which a frontend would mistake for "logged out".
app.Use(async (context, next) =>
{
    if (context.GetEndpoint() is null && context.Request.Path.StartsWithSegments("/api"))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
