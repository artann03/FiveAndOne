using FiveAndOne.Application.Common.Abstractions;
using FiveAndOne.Application.Common.Exceptions;
using FiveAndOne.Application.Common.Settings;
using FiveAndOne.Application.Dtos.Facilities;
using FiveAndOne.Application.Services.Businesses;
using FiveAndOne.Application.Services.Platform;
using FiveAndOne.Domain.Entities.Facilities;
using FiveAndOne.Domain.Enums;
using FiveAndOne.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FiveAndOne.Application.Services.Facilities;

public sealed record UploadedImage(Stream Content, string FileName, string ContentType, long Length);

/// <summary>Upload, remove and reorder facility photos (story 10).</summary>
public interface IFacilityImageService
{
    Task<List<FacilityImageResponse>> UploadAsync(Guid userId, Guid? businessId, Guid facilityId, IReadOnlyList<UploadedImage> files, CancellationToken cancellationToken = default);
    Task<List<FacilityImageResponse>> DeleteAsync(Guid userId, Guid? businessId, Guid facilityId, Guid imageId, CancellationToken cancellationToken = default);
    Task<List<FacilityImageResponse>> ReorderAsync(Guid userId, Guid? businessId, Guid facilityId, List<Guid> imageIds, CancellationToken cancellationToken = default);
}

public class FacilityImageService : IFacilityImageService
{
    // Checked against the file's first bytes, not just the declared content type.
    private static readonly (string ContentType, string Extension, byte[] Magic)[] Formats =
    {
        ("image/jpeg", ".jpg", new byte[] { 0xFF, 0xD8, 0xFF }),
        ("image/png", ".png", new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        ("image/webp", ".webp", new byte[] { 0x52, 0x49, 0x46, 0x46 })   // "RIFF" (+ "WEBP" at offset 8)
    };

    private readonly ApplicationDbContext _db;
    private readonly IBusinessAccess _access;
    private readonly IFileStorage _storage;
    private readonly IAuditService _audit;
    private readonly FacilitySettings _settings;

    public FacilityImageService(ApplicationDbContext db, IBusinessAccess access, IFileStorage storage, IAuditService audit, IOptions<FacilitySettings> settings)
    {
        _db = db;
        _access = access;
        _storage = storage;
        _audit = audit;
        _settings = settings.Value;
    }

    public async Task<List<FacilityImageResponse>> UploadAsync(Guid userId, Guid? businessId, Guid facilityId, IReadOnlyList<UploadedImage> files, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, cancellationToken);

        if (files.Count == 0)
        {
            throw RequestValidationException.For("files", "Choose at least one image.");
        }

        if (facility.Images.Count + files.Count > _settings.MaxImagesPerFacility)
        {
            throw RequestValidationException.For("files",
                $"A facility can have at most {_settings.MaxImagesPerFacility} images ({facility.Images.Count} already added).", "too_many_images");
        }

        // Validate everything first so a bad file doesn't leave half the batch stored.
        var validated = new List<(UploadedImage File, string ContentType, string Extension, byte[] Bytes)>();
        for (var i = 0; i < files.Count; i++)
        {
            var file = files[i];
            if (file.Length <= 0)
                throw RequestValidationException.For($"files[{i}]", $"\"{file.FileName}\" is empty.", "invalid_image");
            if (file.Length > _settings.MaxImageBytes)
                throw RequestValidationException.For($"files[{i}]",
                    $"\"{file.FileName}\" is larger than {_settings.MaxImageBytes / (1024 * 1024)} MB.", "image_too_large");

            using var buffer = new MemoryStream();
            await file.Content.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();

            var format = Formats.FirstOrDefault(f => bytes.Length >= 12 && bytes.AsSpan(0, f.Magic.Length).SequenceEqual(f.Magic)
                                                     && (f.Extension != ".webp" || System.Text.Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP"));
            if (format.ContentType is null)
                throw RequestValidationException.For($"files[{i}]", $"\"{file.FileName}\" must be a JPG, PNG or WebP image.", "invalid_image");

            validated.Add((file, format.ContentType, format.Extension, bytes));
        }

        var order = facility.Images.Count == 0 ? 0 : facility.Images.Max(x => x.SortOrder) + 1;
        foreach (var item in validated)
        {
            await using var stream = new MemoryStream(item.Bytes);
            var url = await _storage.SaveAsync(stream, $"facilities/{facility.Id}", $"{Guid.NewGuid():N}{item.Extension}", item.ContentType, cancellationToken);

            facility.Images.Add(new FacilityImage
            {
                FacilityId = facility.Id,
                Url = url,
                SortOrder = order,
                IsCover = facility.Images.Count == 0
            });
            order++;
        }

        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, "facility.images_uploaded", "Facility", facility.Id.ToString(), $"{validated.Count} image(s)", businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        return FacilityResponse.MapImages(facility);
    }

    public async Task<List<FacilityImageResponse>> DeleteAsync(Guid userId, Guid? businessId, Guid facilityId, Guid imageId, CancellationToken cancellationToken = default)
    {
        var (context, facility) = await LoadAsync(userId, businessId, facilityId, cancellationToken);
        var image = facility.Images.FirstOrDefault(i => i.Id == imageId) ?? throw new NotFoundException("Image not found.");

        if (facility.Status == FacilityStatus.Published && facility.Images.Count == 1)
        {
            throw new ConflictException("A published facility needs at least one image. Add another one first.", "last_image");
        }

        facility.Images.Remove(image);
        Renumber(facility.Images.OrderBy(i => i.SortOrder).ToList());
        facility.UpdatedAt = DateTime.UtcNow;
        _audit.Log(userId, "facility.image_removed", "Facility", facility.Id.ToString(), null, businessId: context.BusinessId);
        await _db.SaveChangesAsync(cancellationToken);

        await _storage.DeleteAsync(image.Url, cancellationToken);
        return FacilityResponse.MapImages(facility);
    }

    public async Task<List<FacilityImageResponse>> ReorderAsync(Guid userId, Guid? businessId, Guid facilityId, List<Guid> imageIds, CancellationToken cancellationToken = default)
    {
        var (_, facility) = await LoadAsync(userId, businessId, facilityId, cancellationToken);

        var current = facility.Images.Select(i => i.Id).ToHashSet();
        if (imageIds.Count != current.Count || !imageIds.All(current.Contains))
        {
            throw RequestValidationException.For("imageIds", "Send every image id of the facility exactly once, in the new order.");
        }

        Renumber(imageIds.Select(id => facility.Images.First(i => i.Id == id)).ToList());
        facility.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return FacilityResponse.MapImages(facility);
    }

    private static void Renumber(List<FacilityImage> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            ordered[i].SortOrder = i;
            ordered[i].IsCover = i == 0;
        }
    }

    private async Task<(BusinessContext, Facility)> LoadAsync(Guid userId, Guid? businessId, Guid facilityId, CancellationToken cancellationToken)
    {
        var context = await _access.GetAsync(userId, businessId, BusinessPermissions.ManageFacilities, cancellationToken);
        var facility = await _db.Facilities.Include(f => f.Images)
            .FirstOrDefaultAsync(f => f.Id == facilityId && f.BusinessId == context.BusinessId, cancellationToken)
            ?? throw new NotFoundException("Facility not found.");
        return (context, facility);
    }
}
