using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Dtos.Common;
using FiveAndOne.Domain.Entities.Platform;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;

namespace FiveAndOne.Application.Services.Platform;

// =====================================================================
// Platform settings (booking limits, grace days, ...)
// =====================================================================

public interface IPlatformSettings
{
    Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default);
    Task<List<PlatformSettingResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PlatformSettingResponse> UpdateAsync(Guid actorId, string key, string value, CancellationToken cancellationToken = default);
}

public class PlatformSettingResponse
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class PlatformSettingsService : IPlatformSettings
{
    private const string CacheKey = "platform-settings";
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _audit;

    public PlatformSettingsService(ApplicationDbContext db, IMemoryCache cache, IAuditService audit)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken cancellationToken = default)
    {
        var all = await LoadAsync(cancellationToken);
        return all.TryGetValue(key, out var value) && int.TryParse(value, out var number) ? number : fallback;
    }

    public async Task<List<PlatformSettingResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _db.PlatformSettings.AsNoTracking()
            .OrderBy(x => x.Key)
            .Select(x => new PlatformSettingResponse { Key = x.Key, Value = x.Value, Description = x.Description, UpdatedAt = x.UpdatedAt })
            .ToListAsync(cancellationToken);
    }

    public async Task<PlatformSettingResponse> UpdateAsync(Guid actorId, string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            ?? throw new NotFoundException("Setting not found.");

        // Every seeded setting is numeric; reject anything else so a typo can't break booking rules.
        if (!int.TryParse(value, out var number) || number < 0)
        {
            throw new RequestValidationException("Value must be a non-negative whole number.",
                new Dictionary<string, string[]> { ["value"] = new[] { "Value must be a non-negative whole number." } });
        }

        var old = setting.Value;
        setting.Value = number.ToString();
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByUserId = actorId;

        _audit.Log(actorId, "platform_setting.updated", "PlatformSetting", key, $"{key}: {old} -> {setting.Value}");
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        return new PlatformSettingResponse { Key = setting.Key, Value = setting.Value, Description = setting.Description, UpdatedAt = setting.UpdatedAt };
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, string>? cached) && cached is not null)
        {
            return cached;
        }

        var values = await _db.PlatformSettings.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.Value, cancellationToken);
        _cache.Set(CacheKey, values, TimeSpan.FromSeconds(30));
        return values;
    }
}

// =====================================================================
// Feature flags (story 49)
// =====================================================================

public interface IFeatureFlags
{
    Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Throws 404 "feature_disabled" when the flag is off.</summary>
    Task EnsureEnabledAsync(string key, CancellationToken cancellationToken = default);

    Task<Dictionary<string, bool>> GetPublicAsync(CancellationToken cancellationToken = default);
    Task<List<FeatureFlagResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<FeatureFlagResponse> CreateAsync(Guid actorId, CreateFeatureFlagRequest request, CancellationToken cancellationToken = default);
    Task<FeatureFlagResponse> SetEnabledAsync(Guid actorId, string key, bool enabled, CancellationToken cancellationToken = default);
}

public class FeatureFlagResponse
{
    public Guid Id { get; set; }
    public string Key { get; set; } = default!;
    public string? Description { get; set; }
    public bool IsEnabled { get; set; }
    public string? ConfigJson { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public static FeatureFlagResponse FromEntity(FeatureFlag f) => new()
    {
        Id = f.Id, Key = f.Key, Description = f.Description, IsEnabled = f.IsEnabled, ConfigJson = f.ConfigJson,
        UpdatedAt = f.UpdatedAt ?? f.CreatedAt
    };
}

public class CreateFeatureFlagRequest
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.RegularExpression("^[a-z0-9_.]{2,100}$", ErrorMessage = "Key must be 2-100 lowercase letters, digits, '_' or '.'.")]
    public string? Key { get; set; }

    [System.ComponentModel.DataAnnotations.StringLength(500)]
    public string? Description { get; set; }

    public bool IsEnabled { get; set; }
}

public class FeatureFlagService : IFeatureFlags
{
    private const string CacheKey = "feature-flags";
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly IAuditService _audit;

    public FeatureFlagService(ApplicationDbContext db, IMemoryCache cache, IAuditService audit)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
    }

    public async Task<bool> IsEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        var flags = await LoadAsync(cancellationToken);
        return flags.TryGetValue(key, out var enabled) && enabled;
    }

    public async Task EnsureEnabledAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(key, cancellationToken))
        {
            throw new FeatureDisabledException(key);
        }
    }

    public Task<Dictionary<string, bool>> GetPublicAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(cancellationToken);

    public async Task<List<FeatureFlagResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var flags = await _db.FeatureFlags.AsNoTracking().OrderBy(x => x.Key).ToListAsync(cancellationToken);
        return flags.Select(FeatureFlagResponse.FromEntity).ToList();
    }

    public async Task<FeatureFlagResponse> CreateAsync(Guid actorId, CreateFeatureFlagRequest request, CancellationToken cancellationToken = default)
    {
        var key = request.Key!.Trim();
        if (await _db.FeatureFlags.AnyAsync(x => x.Key == key, cancellationToken))
        {
            throw new ConflictException("A feature flag with this key already exists.", "duplicate");
        }

        var flag = new FeatureFlag { Key = key, Description = Text.NullIfEmpty(request.Description), IsEnabled = request.IsEnabled, UpdatedByUserId = actorId };
        _db.FeatureFlags.Add(flag);
        _audit.Log(actorId, "feature_flag.created", "FeatureFlag", key, $"Created ({(flag.IsEnabled ? "enabled" : "disabled")})");
        await _db.SaveChangesAsync(cancellationToken);
        _cache.Remove(CacheKey);

        return FeatureFlagResponse.FromEntity(flag);
    }

    public async Task<FeatureFlagResponse> SetEnabledAsync(Guid actorId, string key, bool enabled, CancellationToken cancellationToken = default)
    {
        var flag = await _db.FeatureFlags.FirstOrDefaultAsync(x => x.Key == key, cancellationToken)
            ?? throw new NotFoundException("Feature flag not found.");

        if (flag.IsEnabled != enabled)
        {
            flag.IsEnabled = enabled;
            flag.UpdatedAt = DateTime.UtcNow;
            flag.UpdatedByUserId = actorId;
            _audit.Log(actorId, enabled ? "feature_flag.enabled" : "feature_flag.disabled", "FeatureFlag", key, null);
            await _db.SaveChangesAsync(cancellationToken);
            _cache.Remove(CacheKey);
        }

        return FeatureFlagResponse.FromEntity(flag);
    }

    private async Task<Dictionary<string, bool>> LoadAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out Dictionary<string, bool>? cached) && cached is not null)
        {
            return cached;
        }

        var flags = await _db.FeatureFlags.AsNoTracking().ToDictionaryAsync(x => x.Key, x => x.IsEnabled, cancellationToken);
        _cache.Set(CacheKey, flags, TimeSpan.FromSeconds(30));
        return flags;
    }
}

// =====================================================================
// Audit log (story 50)
// =====================================================================

public interface IAuditService
{
    /// <summary>Adds an audit row to the current unit of work; it is saved with the caller's SaveChanges.</summary>
    void Log(Guid? actorId, string action, string entityType, string? entityId, string? summary, object? data = null, Guid? businessId = null);

    Task<PagedResponse<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default);
}

public class AuditLogQuery
{
    public string? Action { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public Guid? ActorUserId { get; set; }
    public Guid? BusinessId { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AuditLogResponse
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorRole { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? DataJson { get; set; }
    public string? CorrelationId { get; set; }
    public Guid? BusinessId { get; set; }
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IRequestContext _request;

    public AuditService(ApplicationDbContext db, IRequestContext request)
    {
        _db = db;
        _request = request;
    }

    public void Log(Guid? actorId, string action, string entityType, string? entityId, string? summary, object? data = null, Guid? businessId = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            ActorUserId = actorId,
            ActorRole = actorId is null ? "System" : null,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Summary = summary is { Length: > 500 } ? summary[..500] : summary,
            DataJson = data is null ? null : JsonSerializer.Serialize(data),
            CorrelationId = _request.CorrelationId,
            BusinessId = businessId
        });
    }

    public async Task<PagedResponse<AuditLogResponse>> SearchAsync(AuditLogQuery query, CancellationToken cancellationToken = default)
    {
        var (page, pageSize) = Paging.Normalize(query.Page, query.PageSize, 200);
        var q = _db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(x => x.Action.StartsWith(query.Action.Trim()));
        if (!string.IsNullOrWhiteSpace(query.EntityType)) q = q.Where(x => x.EntityType == query.EntityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.EntityId)) q = q.Where(x => x.EntityId == query.EntityId.Trim());
        if (query.ActorUserId.HasValue) q = q.Where(x => x.ActorUserId == query.ActorUserId);
        if (query.BusinessId.HasValue) q = q.Where(x => x.BusinessId == query.BusinessId);
        if (query.From.HasValue) q = q.Where(x => x.CreatedAt >= query.From.Value.ToUniversalTime());
        if (query.To.HasValue) q = q.Where(x => x.CreatedAt < query.To.Value.ToUniversalTime());

        var total = await q.CountAsync(cancellationToken);
        var items = await q.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new AuditLogResponse
            {
                Id = x.Id, CreatedAt = x.CreatedAt, ActorUserId = x.ActorUserId, ActorRole = x.ActorRole, Action = x.Action,
                EntityType = x.EntityType, EntityId = x.EntityId, Summary = x.Summary, DataJson = x.DataJson,
                CorrelationId = x.CorrelationId, BusinessId = x.BusinessId
            })
            .ToListAsync(cancellationToken);

        return PagedResponse<AuditLogResponse>.Create(items, page, pageSize, total);
    }
}

// =====================================================================
// Translations (story 51)
// =====================================================================

public interface ITranslationService
{
    /// <summary>Translated names for the request language: entityId -> name. Missing = use the base (English) name.</summary>
    Task<Dictionary<Guid, string>> GetNamesAsync(string entityType, CancellationToken cancellationToken = default);
    Task SetAsync(string entityType, Guid entityId, string field, IDictionary<string, string> values, CancellationToken cancellationToken = default);
    Task<Dictionary<string, string>> GetAllForAsync(string entityType, Guid entityId, string field, CancellationToken cancellationToken = default);
}

public class TranslationService : ITranslationService
{
    private readonly ApplicationDbContext _db;
    private readonly IRequestContext _request;
    private readonly IMemoryCache _cache;

    public TranslationService(ApplicationDbContext db, IRequestContext request, IMemoryCache cache)
    {
        _db = db;
        _request = request;
        _cache = cache;
    }

    public async Task<Dictionary<Guid, string>> GetNamesAsync(string entityType, CancellationToken cancellationToken = default)
    {
        var language = _request.Language;
        if (language == "en")
        {
            return new Dictionary<Guid, string>();
        }

        var cacheKey = $"translations:{entityType}:{language}";
        if (_cache.TryGetValue(cacheKey, out Dictionary<Guid, string>? cached) && cached is not null)
        {
            return cached;
        }

        var names = await _db.Translations.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.Field == "Name" && x.Language == language)
            .ToDictionaryAsync(x => x.EntityId, x => x.Value, cancellationToken);

        _cache.Set(cacheKey, names, TimeSpan.FromMinutes(5));
        return names;
    }

    public async Task<Dictionary<string, string>> GetAllForAsync(string entityType, Guid entityId, string field, CancellationToken cancellationToken = default)
    {
        return await _db.Translations.AsNoTracking()
            .Where(x => x.EntityType == entityType && x.EntityId == entityId && x.Field == field)
            .ToDictionaryAsync(x => x.Language, x => x.Value, cancellationToken);
    }

    /// <summary>Upserts translations (caller saves). Empty values remove the translation.</summary>
    public async Task SetAsync(string entityType, Guid entityId, string field, IDictionary<string, string> values, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Translations
            .Where(x => x.EntityType == entityType && x.EntityId == entityId && x.Field == field)
            .ToListAsync(cancellationToken);

        foreach (var (language, value) in values)
        {
            var lang = language.Trim().ToLowerInvariant();
            var row = existing.FirstOrDefault(x => x.Language == lang);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (row is not null) _db.Translations.Remove(row);
                continue;
            }

            if (row is null)
            {
                _db.Translations.Add(new Translation { EntityType = entityType, EntityId = entityId, Field = field, Language = lang, Value = value.Trim() });
            }
            else
            {
                row.Value = value.Trim();
                row.UpdatedAt = DateTime.UtcNow;
            }

            _cache.Remove($"translations:{entityType}:{lang}");
        }
    }
}

// =====================================================================
// Notification outbox (emails / SMS)
// =====================================================================

public interface INotificationService
{
    /// <summary>Queues an email (saved with the caller's SaveChanges, sent by the background worker).</summary>
    void QueueEmail(string to, string subject, string body, string template, Guid? businessId = null, Guid? bookingId = null);

    void QueueSms(string phone, string body, string template, Guid? businessId = null, Guid? bookingId = null);

    /// <summary>Sends pending messages. Called by the background worker.</summary>
    Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default);
}

public class NotificationService : INotificationService
{
    private const int MaxAttempts = 5;
    private readonly ApplicationDbContext _db;
    private readonly IEmailSender _email;
    private readonly ISmsSender _sms;

    public NotificationService(ApplicationDbContext db, IEmailSender email, ISmsSender sms)
    {
        _db = db;
        _email = email;
        _sms = sms;
    }

    public void QueueEmail(string to, string subject, string body, string template, Guid? businessId = null, Guid? bookingId = null)
    {
        _db.NotificationMessages.Add(new NotificationMessage
        {
            Channel = NotificationChannel.Email, Recipient = to, Subject = subject, Body = body, Template = template,
            BusinessId = businessId, BookingId = bookingId
        });
    }

    public void QueueSms(string phone, string body, string template, Guid? businessId = null, Guid? bookingId = null)
    {
        _db.NotificationMessages.Add(new NotificationMessage
        {
            Channel = NotificationChannel.Sms, Recipient = phone, Body = body, Template = template,
            BusinessId = businessId, BookingId = bookingId
        });
    }

    public async Task<int> DispatchPendingAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var pending = await _db.NotificationMessages
            .Where(x => x.Status == NotificationStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in pending)
        {
            message.Attempts++;
            try
            {
                if (message.Channel == NotificationChannel.Email)
                {
                    await _email.SendAsync(message.Recipient, message.Subject ?? string.Empty, message.Body, cancellationToken);
                }
                else
                {
                    await _sms.SendAsync(message.Recipient, message.Body, cancellationToken);
                }

                message.Status = NotificationStatus.Sent;
                message.SentAt = DateTime.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (message.Attempts >= MaxAttempts)
                {
                    message.Status = NotificationStatus.Failed;
                }
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return pending.Count;
    }
}
