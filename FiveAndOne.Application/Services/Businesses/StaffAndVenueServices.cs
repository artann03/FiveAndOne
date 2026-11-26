using FiveAndOne.Application.Common;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Businesses;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using FiveAndOne.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Services.Businesses;

// =====================================================================
// Staff (story 57)
// =====================================================================

public class InviteStaffRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    /// <summary>What the staff member may do (flags), e.g. ["ManageReservations", "ManageAvailability"].</summary>
    public BusinessPermissions Permissions { get; set; } = BusinessPermissions.ManageReservations;
}

public class UpdateStaffRequest
{
    public BusinessPermissions Permissions { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AcceptInvitationRequest
{
    [Required]
    public string? Token { get; set; }
}

public class StaffMemberResponse
{
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public BusinessMemberRole Role { get; set; }
    public BusinessPermissions Permissions { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvitationResponse
{
    public Guid Id { get; set; }
    public string Email { get; set; } = default!;
    public BusinessPermissions Permissions { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime LastSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public interface IStaffService
{
    Task<List<StaffMemberResponse>> GetMembersAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<List<InvitationResponse>> GetInvitationsAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<InvitationResponse> InviteAsync(Guid userId, Guid? businessId, InviteStaffRequest request, CancellationToken cancellationToken = default);
    Task<InvitationResponse> ResendAsync(Guid userId, Guid? businessId, Guid invitationId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid userId, Guid? businessId, Guid invitationId, CancellationToken cancellationToken = default);
    Task<StaffMemberResponse> UpdateMemberAsync(Guid userId, Guid? businessId, Guid memberUserId, UpdateStaffRequest request, CancellationToken cancellationToken = default);
    Task<MyBusinessResponseLite> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default);
}

public class MyBusinessResponseLite
{
    public Guid BusinessId { get; set; }
    public string BusinessName { get; set; } = default!;
    public BusinessPermissions Permissions { get; set; }
}

public class StaffService : IStaffService
{
    private const int InvitationDays = 7;

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IFeatureFlags _flags;
    private readonly INotificationService _notifications;
    private readonly IAuditService _audit;
    private readonly AppSettings _app;

    public StaffService(ApplicationDbContext db, IBusinessAccess access, IFeatureFlags flags, INotificationService notifications, IAuditService audit, IOptions<AppSettings> app)
    {
        _db = db;
        _access = access;
        _flags = flags;
        _notifications = notifications;
        _audit = audit;
        _app = app.Value;
    }

    public async Task<List<StaffMemberResponse>> GetMembersAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        return await _db.BusinessMembers.AsNoTracking().Where(m => m.BusinessId == context.BusinessId)
            .OrderBy(m => m.Role).ThenBy(m => m.CreatedAt)
            .Select(m => new StaffMemberResponse
            {
                UserId = m.UserId,
                Name = (m.User.FirstName + " " + (m.User.LastName ?? "")).Trim(),
                Email = m.User.Email,
                Role = m.Role,
                Permissions = m.Role == BusinessMemberRole.Owner ? BusinessPermissions.All : m.Permissions,
                IsActive = m.IsActive,
                CreatedAt = m.CreatedAt
            }).ToListAsync(cancellationToken);
    }

    public async Task<List<InvitationResponse>> GetInvitationsAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageStaff, cancellationToken);
        var rows = await _db.BusinessInvitations.AsNoTracking().Where(i => i.BusinessId == context.BusinessId)
            .OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<InvitationResponse> InviteAsync(Guid userId, Guid? businessId, InviteStaffRequest request, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.StaffInvitations, cancellationToken);
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageStaff, cancellationToken);
        var email = Text.NormalizeEmail(request.Email!);
        var permissions = request.Permissions & BusinessPermissions.All;

        // Only owners can hand out staff management.
        if (!context.IsOwner && permissions.HasFlag(BusinessPermissions.ManageStaff))
        {
            throw new ForbiddenException("Only the owner can give staff management permission.", "missing_permission");
        }

        if (await _db.BusinessMembers.AnyAsync(m => m.BusinessId == context.BusinessId && m.User.Email == email, cancellationToken))
        {
            throw new ConflictException("This person is already a member of the business.", "already_member");
        }

        var pending = await _db.BusinessInvitations.Where(i => i.BusinessId == context.BusinessId && i.Email == email && i.Status == InvitationStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var old in pending) old.Status = InvitationStatus.Revoked;

        var token = SecureTokens.Create();
        var invitation = new BusinessInvitation
        {
            BusinessId = context.BusinessId,
            Email = email,
            Permissions = permissions,
            TokenHash = SecureTokens.Hash(token),
            ExpiresAt = DateTime.UtcNow.AddDays(InvitationDays),
            InvitedByUserId = userId
        };
        _db.BusinessInvitations.Add(invitation);
        SendInvitation(context.Business, invitation, token);
        _audit.Log(userId, "staff.invited", "BusinessInvitation", invitation.Id.ToString(), $"{email} ({permissions})", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(invitation);
    }

    public async Task<InvitationResponse> ResendAsync(Guid userId, Guid? businessId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.StaffInvitations, cancellationToken);
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageStaff, cancellationToken);
        var invitation = await _db.BusinessInvitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");
        if (invitation.Status is not (InvitationStatus.Pending or InvitationStatus.Expired))
        {
            throw new ConflictException("This invitation can no longer be resent.", "invalid_status");
        }

        // A new token: the previous link stops working.
        var token = SecureTokens.Create();
        invitation.TokenHash = SecureTokens.Hash(token);
        invitation.Status = InvitationStatus.Pending;
        invitation.ExpiresAt = DateTime.UtcNow.AddDays(InvitationDays);
        invitation.LastSentAt = DateTime.UtcNow;
        SendInvitation(context.Business, invitation, token);
        await _db.SaveChangesAsync(cancellationToken);
        return ToResponse(invitation);
    }

    public async Task RevokeAsync(Guid userId, Guid? businessId, Guid invitationId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageStaff, cancellationToken);
        var invitation = await _db.BusinessInvitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");
        if (invitation.Status == InvitationStatus.Pending)
        {
            invitation.Status = InvitationStatus.Revoked;
            _audit.Log(userId, "staff.invitation_revoked", "BusinessInvitation", invitation.Id.ToString(), invitation.Email, businessId: context.BusinessId);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<StaffMemberResponse> UpdateMemberAsync(Guid userId, Guid? businessId, Guid memberUserId, UpdateStaffRequest request, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageStaff, cancellationToken);
        var member = await _db.BusinessMembers.Include(m => m.User)
            .FirstOrDefaultAsync(m => m.BusinessId == context.BusinessId && m.UserId == memberUserId, cancellationToken)
            ?? throw new NotFoundException("Staff member not found.");

        if (member.Role == BusinessMemberRole.Owner)
        {
            throw new ForbiddenException("The owner's access can't be changed.", "owner_protected");
        }
        if (!context.IsOwner && request.Permissions.HasFlag(BusinessPermissions.ManageStaff))
        {
            throw new ForbiddenException("Only the owner can give staff management permission.", "missing_permission");
        }

        member.Permissions = request.Permissions & BusinessPermissions.All;
        member.IsActive = request.IsActive;
        member.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, request.IsActive ? "staff.updated" : "staff.deactivated", "BusinessMember", member.Id.ToString(),
            $"{member.User.Email}: {member.Permissions}", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return new StaffMemberResponse
        {
            UserId = member.UserId, Name = $"{member.User.FirstName} {member.User.LastName}".Trim(), Email = member.User.Email,
            Role = member.Role, Permissions = member.Permissions, IsActive = member.IsActive, CreatedAt = member.CreatedAt
        };
    }

    public async Task<MyBusinessResponseLite> AcceptAsync(Guid userId, string token, CancellationToken cancellationToken = default)
    {
        var hash = SecureTokens.Hash(token.Trim());
        var invitation = await _db.BusinessInvitations.Include(i => i.Business).FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken)
            ?? throw new NotFoundException("Invitation not found.");
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken) ?? throw new UnauthorizedException("User no longer exists.");

        if (invitation.Status != InvitationStatus.Pending || invitation.ExpiresAt < DateTime.UtcNow)
        {
            if (invitation.Status == InvitationStatus.Pending) invitation.Status = InvitationStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            throw new ConflictException("This invitation has expired or was revoked. Ask for a new one.", "invitation_invalid");
        }

        // The invitation is personal: it only works for the invited email address.
        if (!string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("This invitation was sent to a different email address.", "invitation_email_mismatch");
        }

        var member = await _db.BusinessMembers.FirstOrDefaultAsync(m => m.BusinessId == invitation.BusinessId && m.UserId == userId, cancellationToken);
        if (member is null)
        {
            member = new BusinessMember { BusinessId = invitation.BusinessId, UserId = userId, Role = BusinessMemberRole.Staff };
            _db.BusinessMembers.Add(member);
        }
        member.Permissions = invitation.Permissions;
        member.IsActive = true;

        invitation.Status = InvitationStatus.Accepted;
        invitation.AcceptedAt = DateTime.UtcNow;
        invitation.AcceptedByUserId = userId;
        _audit.Log(userId, "staff.invitation_accepted", "BusinessInvitation", invitation.Id.ToString(), user.Email, businessId: invitation.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return new MyBusinessResponseLite { BusinessId = invitation.BusinessId, BusinessName = invitation.Business.BusinessName, Permissions = member.Permissions };
    }

    private void SendInvitation(Business business, BusinessInvitation invitation, string token)
    {
        var link = $"{_app.FrontendBaseUrl}/invitations/accept?token={Uri.EscapeDataString(token)}";
        _notifications.QueueEmail(invitation.Email, $"You're invited to manage {business.BusinessName} on SportX",
            $"{business.BusinessName} invited you to help manage their facilities on SportX.\n\n" +
            $"Accept the invitation (valid until {invitation.ExpiresAt:dd.MM.yyyy}): {link}\n\n" +
            "Log in or create an account with this email address first.",
            "business.staff_invitation", business.Id);
    }

    private static InvitationResponse ToResponse(BusinessInvitation i) => new()
    {
        Id = i.Id,
        Email = i.Email,
        Permissions = i.Permissions,
        Status = i.Status == InvitationStatus.Pending && i.ExpiresAt < DateTime.UtcNow ? InvitationStatus.Expired : i.Status,
        ExpiresAt = i.ExpiresAt,
        LastSentAt = i.LastSentAt,
        CreatedAt = i.CreatedAt
    };
}

// =====================================================================
// Venues (story 59)
// =====================================================================

public class SaveVenueRequest
{
    [Required, StringLength(150)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(250)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [Range(-90, 90)]
    public decimal? Latitude { get; set; }

    [Range(-180, 180)]
    public decimal? Longitude { get; set; }

    [StringLength(500)]
    public string? CoverImageUrl { get; set; }

    /// <summary>Courts (facilities of this business) that belong to the venue. Replaces the current list.</summary>
    public List<Guid> FacilityIds { get; set; } = new();
}

public class VenueResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string Slug { get; set; } = default!;
    public string? Description { get; set; }
    public string? AddressLine { get; set; }
    public string? City { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CoverImageUrl { get; set; }
    public List<Guid> FacilityIds { get; set; } = new();
}

public interface IVenueService
{
    Task<List<VenueResponse>> GetAllAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default);
    Task<VenueResponse> SaveAsync(Guid userId, Guid? businessId, Guid? venueId, SaveVenueRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid? businessId, Guid venueId, CancellationToken cancellationToken = default);
}

public class VenueService : IVenueService
{
    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IFeatureFlags _flags;

    public VenueService(ApplicationDbContext db, IBusinessAccess access, IFeatureFlags flags)
    {
        _db = db;
        _access = access;
        _flags = flags;
    }

    public async Task<List<VenueResponse>> GetAllAsync(Guid userId, Guid? businessId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.None, cancellationToken);
        var venues = await _db.Venues.AsNoTracking().Include(v => v.Facilities).Where(v => v.BusinessId == context.BusinessId)
            .OrderBy(v => v.Name).ToListAsync(cancellationToken);
        return venues.Select(ToResponse).ToList();
    }

    public async Task<VenueResponse> SaveAsync(Guid userId, Guid? businessId, Guid? venueId, SaveVenueRequest request, CancellationToken cancellationToken = default)
    {
        await _flags.EnsureEnabledAsync(FeatureFlagKeys.Venues, cancellationToken);
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);

        Venue venue;
        if (venueId.HasValue)
        {
            venue = await _db.Venues.Include(v => v.Facilities).FirstOrDefaultAsync(v => v.Id == venueId && v.BusinessId == context.BusinessId, cancellationToken)
                ?? throw new NotFoundException("Venue not found.");
            venue.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            venue = new Venue { BusinessId = context.BusinessId };
            venue.Slug = Slugs.Create(request.Name!, venue.Id, "venue");
            _db.Venues.Add(venue);
        }

        var facilityIds = request.FacilityIds.Distinct().ToList();
        var facilities = await _db.Facilities.Where(f => f.BusinessId == context.BusinessId && (facilityIds.Contains(f.Id) || f.VenueId == venue.Id))
            .ToListAsync(cancellationToken);
        if (facilities.Count(f => facilityIds.Contains(f.Id)) != facilityIds.Count)
        {
            throw RequestValidationException.For("facilityIds", "One or more facilities were not found.");
        }

        venue.Name = request.Name!.Trim();
        venue.Description = Text.NullIfEmpty(request.Description);
        venue.AddressLine = Text.NullIfEmpty(request.AddressLine);
        venue.City = Text.NullIfEmpty(request.City);
        venue.Latitude = request.Latitude;
        venue.Longitude = request.Longitude;
        venue.CoverImageUrl = Text.NullIfEmpty(request.CoverImageUrl);

        foreach (var facility in facilities)
        {
            facility.VenueId = facilityIds.Contains(facility.Id) ? venue.Id : null;
        }

        await _db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(venue);
        response.FacilityIds = facilityIds;
        return response;
    }

    public async Task DeleteAsync(Guid userId, Guid? businessId, Guid venueId, CancellationToken cancellationToken = default)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == venueId && v.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Venue not found.");

        // Courts stay; they just no longer belong to a venue (FK is SET NULL).
        _db.Venues.Remove(venue);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static VenueResponse ToResponse(Venue v) => new()
    {
        Id = v.Id, Name = v.Name, Slug = v.Slug, Description = v.Description, AddressLine = v.AddressLine, City = v.City,
        Latitude = v.Latitude, Longitude = v.Longitude, CoverImageUrl = v.CoverImageUrl,
        FacilityIds = v.Facilities.Select(f => f.Id).ToList()
    };
}
