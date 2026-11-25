namespace FiveAndOne.Domain.Enums;

// Draft -> Published <-> Hidden. Only Published is visible to the public.
// Suspended is set by an admin; the owner can't publish until an admin lifts it.
public enum FacilityStatus
{
    Draft = 1,
    Published = 2,
    Hidden = 3,
    Suspended = 4
}

public enum FacilityType
{
    Court = 1,
    Pitch = 2,
    Field = 3,
    Lane = 4,
    Course = 5,
    Range = 6,
    Pool = 7,
    Room = 8,
    Other = 9
}

public enum ConfirmationMode
{
    Automatic = 1,
    Manual = 2
}

public enum BookingMode
{
    FixedSlots = 1,   // fixed length, e.g. 60-minute futsal slots
    Flexible = 2,     // customer picks a length between min and max, in steps
    Session = 3       // pre-scheduled sessions with several spots (FacilitySessions)
}

public enum PriceType
{
    PerHour = 1,
    PerSlot = 2,
    PerPerson = 3
}

public enum FacilitySessionStatus
{
    Scheduled = 1,
    Cancelled = 2
}

public enum AvailabilityBlockType
{
    Closure = 1,
    Maintenance = 2,
    Holiday = 3,
    Blocked = 4,
    SpecialOpening = 5
}

[Flags]
public enum FacilityPermissions
{
    None = 0,
    ManageBookings = 1,
    ManageAvailability = 2,
    ManageResources = 4,
    ManagePricing = 8,
    ViewReports = 16,
    All = ManageBookings | ManageAvailability | ManageResources | ManagePricing | ViewReports
}
