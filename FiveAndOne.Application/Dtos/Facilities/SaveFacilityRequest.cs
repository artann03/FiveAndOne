using FiveAndOne.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FiveAndOne.Application.Dtos.Facilities;

/// <summary>
/// Body for "Add facility" / "Save draft" (create) and edit.
/// Saving a draft only needs a name; everything else is checked when publishing.
/// </summary>
public class SaveFacilityRequest
{
    [Required(ErrorMessage = "Facility name is required.")]
    [StringLength(150, ErrorMessage = "Facility name can be at most 150 characters.")]
    public string? Name { get; set; }

    [StringLength(2000, ErrorMessage = "Description can be at most 2000 characters.")]
    public string? Description { get; set; }

    /// <summary>Court, Pitch, Field, Lane, Course, Range, Pool, Room, Other.</summary>
    public FacilityType? Type { get; set; }

    /// <summary>Sports that can be played here (ids from GET /api/sports).</summary>
    public List<Guid> SportIds { get; set; } = new();

    /// <summary>Max number of people, where relevant.</summary>
    [Range(1, 10000, ErrorMessage = "Capacity must be between 1 and 10000.")]
    public int? Capacity { get; set; }

    public bool IsIndoor { get; set; }

    [StringLength(50, ErrorMessage = "Surface can be at most 50 characters.")]
    public string? Surface { get; set; }

    [StringLength(250, ErrorMessage = "Address can be at most 250 characters.")]
    public string? AddressLine { get; set; }

    [StringLength(100, ErrorMessage = "City can be at most 100 characters.")]
    public string? City { get; set; }

    [RegularExpression("^[A-Za-z]{2}$", ErrorMessage = "Country must be a 2-letter ISO code, e.g. XK for Kosovo.")]
    public string? Country { get; set; }

    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90.")]
    public decimal? Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180.")]
    public decimal? Longitude { get; set; }

    /// <summary>Characteristics / amenities (ids from GET /api/amenities).</summary>
    public List<Guid> AmenityIds { get; set; } = new();

    /// <summary>Image URLs in display order; the first one is the cover image.</summary>
    public List<string> ImageUrls { get; set; } = new();
}
