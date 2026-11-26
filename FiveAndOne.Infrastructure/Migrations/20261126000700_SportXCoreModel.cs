using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FiveAndOne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SportXCoreModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // HAND EDIT: the no-overlap constraint references SessionId (renamed/dropped below). Recreated at the end.
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"EX_Bookings_NoOverlap\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_FacilitySessions_SessionId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Users_OwnerUserId",
                table: "Facilities");

            migrationBuilder.DropTable(
                name: "BookingStatusHistory");

            // HAND EDIT: "BusinessProfiles" is copied into "Businesses" further down before it is dropped.

            migrationBuilder.DropTable(
                name: "FacilityMembers");

            migrationBuilder.DropTable(
                name: "FacilitySessions");

            migrationBuilder.DropIndex(
                name: "IX_FacilityPriceRules_FacilityId_DayOfWeek",
                table: "FacilityPriceRules");

            migrationBuilder.DropIndex(
                name: "IX_FacilityOperatingHours_FacilityId_DayOfWeek",
                table: "FacilityOperatingHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Durations",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_CustomerUserId_IdempotencyKey",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_FacilityId_Status_StartsAt",
                table: "Bookings");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Bookings_Spots",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ValidFrom",
                table: "FacilityPriceRules");

            migrationBuilder.DropColumn(
                name: "ValidTo",
                table: "FacilityPriceRules");

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DurationStepMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MaxDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MinDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "SlotDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "ConfirmationMode",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "AvailabilityBlocks");

            migrationBuilder.RenameColumn(
                name: "OwnerUserId",
                table: "Facilities",
                newName: "BusinessId");

            migrationBuilder.RenameColumn(
                name: "ConfirmationMode",
                table: "Facilities",
                newName: "SlotStepMinutes");

            migrationBuilder.RenameIndex(
                name: "IX_Facilities_OwnerUserId_Status",
                table: "Facilities",
                newName: "IX_Facilities_BusinessId_Status");

            migrationBuilder.RenameColumn(
                name: "Spots",
                table: "Bookings",
                newName: "Source");

            migrationBuilder.RenameColumn(
                name: "SessionId",
                table: "Bookings",
                newName: "CreatedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_SessionId",
                table: "Bookings",
                newName: "IX_Bookings_CreatedByUserId");

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndsAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            // HAND EDIT: existing users get Albanian and a private profile.
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "Users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "sq");

            migrationBuilder.AddColumn<int>(
                name: "ProfileVisibility",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                table: "FacilitySports",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "DurationOptions",
                table: "FacilitySports",
                type: "integer[]",
                nullable: true);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "StartTime",
                table: "FacilityPriceRules",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "EndTime",
                table: "FacilityPriceRules",
                type: "time without time zone",
                nullable: true,
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone");

            migrationBuilder.AddColumn<int>(
                name: "DurationMinutes",
                table: "FacilityPriceRules",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Label",
                table: "FacilityPriceRules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SportId",
                table: "FacilityPriceRules",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SportId",
                table: "FacilityOperatingHours",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CancellationCutoffHours",
                table: "Facilities",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<string>(
                name: "CancellationPolicyNote",
                table: "Facilities",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int[]>(
                name: "DurationOptions",
                table: "Facilities",
                type: "integer[]",
                nullable: false,
                defaultValue: new[] { 60 });   // HAND EDIT: existing facilities keep 60-minute slots

            migrationBuilder.AddColumn<bool>(
                name: "RequiresOnlinePayment",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "VenueId",
                table: "Facilities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerUserId",
                table: "Bookings",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "BusinessId",
                table: "Bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "ContactEmail",
                table: "Bookings",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                table: "Bookings",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ContactPhone",
                table: "Bookings",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceHash",
                table: "Bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExternalChannel",
                table: "Bookings",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GuestAccessTokenHash",
                table: "Bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotes",
                table: "Bookings",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IpHash",
                table: "Bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsGuest",
                table: "Bookings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // HAND EDIT: text -> enum can't be cast; replace the column (existing blocks become "Other").
            migrationBuilder.DropColumn(
                name: "Reason",
                table: "AvailabilityBlocks");

            migrationBuilder.AddColumn<int>(
                name: "Reason",
                table: "AvailabilityBlocks",
                type: "integer",
                nullable: false,
                defaultValue: 4);

            migrationBuilder.AddColumn<string>(
                name: "InternalNote",
                table: "AvailabilityBlocks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SportId",
                table: "AvailabilityBlocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DataJson = table.Column<string>(type: "jsonb", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BookingHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<int>(type: "integer", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: true),
                    OldStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OldEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewStartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    NewEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Businesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    RegistrationNumber = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspensionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    InactiveSince = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RetentionUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AnonymizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancellationCutoffHours = table.Column<int>(type: "integer", nullable: false),
                    CancellationPolicyNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Businesses", x => x.Id);
                    table.CheckConstraint("CK_Businesses_CancellationCutoff", "\"CancellationCutoffHours\" >= 0");
                    table.ForeignKey(
                        name: "FK_Businesses_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ContentReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReporterUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ResolvedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContentReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FacilityPromotions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Label = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityPromotions", x => x.Id);
                    table.CheckConstraint("CK_FacilityPromotions_Time", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_FacilityPromotions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityScheduleExceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityScheduleExceptions", x => x.Id);
                    table.CheckConstraint("CK_FacilityScheduleExceptions_Dates", "\"EndDate\" >= \"StartDate\"");
                    table.CheckConstraint("CK_FacilityScheduleExceptions_Hours", "\"IsClosed\" OR (\"OpensAt\" IS NOT NULL AND \"ClosesAt\" IS NOT NULL AND \"OpensAt\" <> \"ClosesAt\")");
                    table.ForeignKey(
                        name: "FK_FacilityScheduleExceptions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityScheduleExceptions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "FeatureFlags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: true),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FeatureFlags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NotificationMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    Recipient = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Body = table.Column<string>(type: "text", nullable: false),
                    Template = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PhoneVerifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    CodeHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    VerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    VerificationTokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TokenUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhoneVerifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PlatformSettings",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlatformSettings", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "Reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModerationNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BusinessReply = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    BusinessRepliedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reviews", x => x.Id);
                    table.CheckConstraint("CK_Reviews_Rating", "\"Rating\" BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_Reviews_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Reviews_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    BillingInterval = table.Column<int>(type: "integer", nullable: false),
                    MaxFacilities = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                    table.CheckConstraint("CK_SubscriptionPlans_MaxFacilities", "\"MaxFacilities\" >= 0");
                    table.CheckConstraint("CK_SubscriptionPlans_Price", "\"Price\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Slug = table.Column<string>(type: "character varying(130)", maxLength: 130, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    SportId = table.Column<Guid>(type: "uuid", nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    MaxMembers = table.Column<int>(type: "integer", nullable: false),
                    IsOpenToRequests = table.Column<bool>(type: "boolean", nullable: false),
                    CaptainUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.CheckConstraint("CK_Teams_MaxMembers", "\"MaxMembers\" BETWEEN 2 AND 100");
                    table.ForeignKey(
                        name: "FK_Teams_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Teams_Users_CaptainUserId",
                        column: x => x.CaptainUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Translations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Field = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Value = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Translations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BusinessInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceptedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessInvitations_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessMembers_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BusinessMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Venues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Slug = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    AddressLine = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Latitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "numeric(9,6)", precision: 9, scale: 6, nullable: true),
                    CoverImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Venues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Venues_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AnchorDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GraceEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GraceNoticeSent = table.Column<bool>(type: "boolean", nullable: false),
                    CancelAtPeriodEnd = table.Column<bool>(type: "boolean", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessSubscriptions_Businesses_BusinessId",
                        column: x => x.BusinessId,
                        principalTable: "Businesses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BusinessSubscriptions_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    BusinessId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubscriptionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    PayerUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ProviderReference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CheckoutUrl = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RefundedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.CheckConstraint("CK_Payments_Amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_Payments_BusinessSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "BusinessSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "FeatureFlags",
                columns: new[] { "Id", "ConfigJson", "CreatedAt", "Description", "IsEnabled", "Key", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { new Guid("40000000-0000-0000-0000-000000000001"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Guests can reserve without an account.", true, "guest_booking", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000002"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Email confirmations/updates to customers (post-MVP).", false, "customer_email_notifications", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000003"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "SMS notifications to customers (post-MVP).", false, "sms_notifications", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000004"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Risk-triggered SMS phone verification for guest bookings (post-MVP).", false, "sms_verification", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000005"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Online payment for reservations (post-MVP).", false, "customer_payments", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000006"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Ratings and reviews (post-MVP).", false, "reviews", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000007"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Customer player profiles (post-MVP).", false, "player_profiles", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000008"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Teammate discovery and teams (post-MVP).", false, "teams", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000009"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "One user managing several businesses (needs product approval).", false, "multiple_businesses", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000010"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Invite staff with permissions (post-MVP).", false, "staff_invitations", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000011"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Parent venues grouping several courts (post-MVP).", false, "venues", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000012"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Personal facility recommendations (post-MVP).", false, "recommendations", null, null },
                    { new Guid("40000000-0000-0000-0000-000000000013"), null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Sponsored facilities in discovery (post-MVP).", false, "promoted_listings", null, null }
                });

            migrationBuilder.InsertData(
                table: "PlatformSettings",
                columns: new[] { "Key", "Description", "UpdatedAt", "UpdatedByUserId", "Value" },
                values: new object[,]
                {
                    { "booking.max_active_reservations", "Max upcoming reservations a customer can hold at a time.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "2" },
                    { "booking.max_reservations_per_day", "Max reservations a customer can hold on the same day (cancelled ones don't count).", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "2" },
                    { "guest.max_attempts_per_hour", "Guest booking attempts per IP/device per hour before requests are refused.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "10" },
                    { "guest.max_reservations_per_day", "Max guest reservations per phone number / device per day.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "2" },
                    { "reviews.window_days", "Days after a completed reservation during which a review can be written.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "30" },
                    { "subscription.grace_days", "Days a business keeps full access after a failed subscription renewal.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "14" },
                    { "subscription.retention_years", "Years an unpaid (inactive) business's data is kept before anonymization.", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, "2" }
                });

            migrationBuilder.UpdateData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "Category", "DefaultMaxParticipants", "Name", "Slug" },
                values: new object[] { 3, 20, "Fitness", "fitness" });

            migrationBuilder.InsertData(
                table: "Sports",
                columns: new[] { "Id", "Category", "CreatedAt", "DefaultMaxParticipants", "DefaultMinParticipants", "Description", "IconUrl", "IsActive", "MetadataJson", "Name", "Slug", "SortOrder", "UpdatedAt" },
                values: new object[] { new Guid("10000000-0000-0000-0000-000000000011"), 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 20, 1, null, null, true, null, "Martial Arts", "martial-arts", 11, null });

            migrationBuilder.InsertData(
                table: "Translations",
                columns: new[] { "Id", "CreatedAt", "EntityId", "EntityType", "Field", "Language", "UpdatedAt", "Value" },
                values: new object[,]
                {
                    { new Guid("30000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000001"), "Sport", "Name", "sq", null, "Futboll" },
                    { new Guid("30000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000001"), "Sport", "Name", "tr", null, "Futbol" },
                    { new Guid("30000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000001"), "Sport", "Name", "sr", null, "Fudbal" },
                    { new Guid("30000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000002"), "Sport", "Name", "sq", null, "Futsall" },
                    { new Guid("30000000-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000002"), "Sport", "Name", "tr", null, "Futsal" },
                    { new Guid("30000000-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000002"), "Sport", "Name", "sr", null, "Futsal" },
                    { new Guid("30000000-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000003"), "Sport", "Name", "sq", null, "Basketboll" },
                    { new Guid("30000000-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000003"), "Sport", "Name", "tr", null, "Basketbol" },
                    { new Guid("30000000-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000003"), "Sport", "Name", "sr", null, "Košarka" },
                    { new Guid("30000000-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000004"), "Sport", "Name", "sq", null, "Tenis" },
                    { new Guid("30000000-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000004"), "Sport", "Name", "tr", null, "Tenis" },
                    { new Guid("30000000-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000004"), "Sport", "Name", "sr", null, "Tenis" },
                    { new Guid("30000000-0000-0000-0000-000000000013"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000005"), "Sport", "Name", "sq", null, "Padel" },
                    { new Guid("30000000-0000-0000-0000-000000000014"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000005"), "Sport", "Name", "tr", null, "Padel" },
                    { new Guid("30000000-0000-0000-0000-000000000015"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000005"), "Sport", "Name", "sr", null, "Padel" },
                    { new Guid("30000000-0000-0000-0000-000000000016"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000006"), "Sport", "Name", "sq", null, "Fitnes" },
                    { new Guid("30000000-0000-0000-0000-000000000017"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000006"), "Sport", "Name", "tr", null, "Fitness" },
                    { new Guid("30000000-0000-0000-0000-000000000018"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000006"), "Sport", "Name", "sr", null, "Fitnes" },
                    { new Guid("30000000-0000-0000-0000-000000000019"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000007"), "Sport", "Name", "sq", null, "Volejboll" },
                    { new Guid("30000000-0000-0000-0000-000000000020"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000007"), "Sport", "Name", "tr", null, "Voleybol" },
                    { new Guid("30000000-0000-0000-0000-000000000021"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000007"), "Sport", "Name", "sr", null, "Odbojka" },
                    { new Guid("30000000-0000-0000-0000-000000000022"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000008"), "Sport", "Name", "sq", null, "Badminton" },
                    { new Guid("30000000-0000-0000-0000-000000000023"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000008"), "Sport", "Name", "tr", null, "Badminton" },
                    { new Guid("30000000-0000-0000-0000-000000000024"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000008"), "Sport", "Name", "sr", null, "Badminton" },
                    { new Guid("30000000-0000-0000-0000-000000000025"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000009"), "Sport", "Name", "sq", null, "Not" },
                    { new Guid("30000000-0000-0000-0000-000000000026"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000009"), "Sport", "Name", "tr", null, "Yüzme" },
                    { new Guid("30000000-0000-0000-0000-000000000027"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000009"), "Sport", "Name", "sr", null, "Plivanje" },
                    { new Guid("30000000-0000-0000-0000-000000000028"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000010"), "Sport", "Name", "sq", null, "Pingpong" },
                    { new Guid("30000000-0000-0000-0000-000000000029"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000010"), "Sport", "Name", "tr", null, "Masa tenisi" },
                    { new Guid("30000000-0000-0000-0000-000000000030"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000010"), "Sport", "Name", "sr", null, "Stoni tenis" },
                    { new Guid("30000000-0000-0000-0000-000000000031"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000011"), "Sport", "Name", "sq", null, "Arte marciale" },
                    { new Guid("30000000-0000-0000-0000-000000000032"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000011"), "Sport", "Name", "tr", null, "Dövüş sanatları" },
                    { new Guid("30000000-0000-0000-0000-000000000033"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("10000000-0000-0000-0000-000000000011"), "Sport", "Name", "sr", null, "Borilačke veštine" },
                    { new Guid("30000000-0000-0000-0000-000000000034"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000001"), "Amenity", "Name", "sq", null, "Parking" },
                    { new Guid("30000000-0000-0000-0000-000000000035"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000001"), "Amenity", "Name", "tr", null, "Otopark" },
                    { new Guid("30000000-0000-0000-0000-000000000036"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000001"), "Amenity", "Name", "sr", null, "Parking" },
                    { new Guid("30000000-0000-0000-0000-000000000037"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000002"), "Amenity", "Name", "sq", null, "Dhoma zhveshjeje" },
                    { new Guid("30000000-0000-0000-0000-000000000038"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000002"), "Amenity", "Name", "tr", null, "Soyunma odaları" },
                    { new Guid("30000000-0000-0000-0000-000000000039"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000002"), "Amenity", "Name", "sr", null, "Svlačionice" },
                    { new Guid("30000000-0000-0000-0000-000000000040"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000003"), "Amenity", "Name", "sq", null, "Dushe" },
                    { new Guid("30000000-0000-0000-0000-000000000041"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000003"), "Amenity", "Name", "tr", null, "Duşlar" },
                    { new Guid("30000000-0000-0000-0000-000000000042"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000003"), "Amenity", "Name", "sr", null, "Tuševi" },
                    { new Guid("30000000-0000-0000-0000-000000000043"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000004"), "Amenity", "Name", "sq", null, "Dollapë" },
                    { new Guid("30000000-0000-0000-0000-000000000044"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000004"), "Amenity", "Name", "tr", null, "Dolaplar" },
                    { new Guid("30000000-0000-0000-0000-000000000045"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000004"), "Amenity", "Name", "sr", null, "Ormarići" },
                    { new Guid("30000000-0000-0000-0000-000000000046"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000005"), "Amenity", "Name", "sq", null, "Ndriçim" },
                    { new Guid("30000000-0000-0000-0000-000000000047"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000005"), "Amenity", "Name", "tr", null, "Projektör aydınlatma" },
                    { new Guid("30000000-0000-0000-0000-000000000048"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000005"), "Amenity", "Name", "sr", null, "Reflektori" },
                    { new Guid("30000000-0000-0000-0000-000000000049"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000006"), "Amenity", "Name", "sq", null, "Kafene / Bar" },
                    { new Guid("30000000-0000-0000-0000-000000000050"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000006"), "Amenity", "Name", "tr", null, "Kafe / Bar" },
                    { new Guid("30000000-0000-0000-0000-000000000051"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000006"), "Amenity", "Name", "sr", null, "Kafić / Bar" },
                    { new Guid("30000000-0000-0000-0000-000000000052"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000007"), "Amenity", "Name", "sq", null, "Qiramarrje pajisjesh" },
                    { new Guid("30000000-0000-0000-0000-000000000053"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000007"), "Amenity", "Name", "tr", null, "Ekipman kiralama" },
                    { new Guid("30000000-0000-0000-0000-000000000054"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000007"), "Amenity", "Name", "sr", null, "Iznajmljivanje opreme" },
                    { new Guid("30000000-0000-0000-0000-000000000055"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000008"), "Amenity", "Name", "sq", null, "Ulëse për spektatorë" },
                    { new Guid("30000000-0000-0000-0000-000000000056"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000008"), "Amenity", "Name", "tr", null, "Seyirci oturma alanı" },
                    { new Guid("30000000-0000-0000-0000-000000000057"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000008"), "Amenity", "Name", "sr", null, "Mesta za gledaoce" },
                    { new Guid("30000000-0000-0000-0000-000000000058"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000009"), "Amenity", "Name", "sq", null, "Wi-Fi" },
                    { new Guid("30000000-0000-0000-0000-000000000059"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000009"), "Amenity", "Name", "tr", null, "Wi-Fi" },
                    { new Guid("30000000-0000-0000-0000-000000000060"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000009"), "Amenity", "Name", "sr", null, "Wi-Fi" },
                    { new Guid("30000000-0000-0000-0000-000000000061"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000010"), "Amenity", "Name", "sq", null, "Kondicioner" },
                    { new Guid("30000000-0000-0000-0000-000000000062"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000010"), "Amenity", "Name", "tr", null, "Klima" },
                    { new Guid("30000000-0000-0000-0000-000000000063"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000010"), "Amenity", "Name", "sr", null, "Klima uređaj" },
                    { new Guid("30000000-0000-0000-0000-000000000064"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000011"), "Amenity", "Name", "sq", null, "Ngrohje" },
                    { new Guid("30000000-0000-0000-0000-000000000065"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000011"), "Amenity", "Name", "tr", null, "Isıtma" },
                    { new Guid("30000000-0000-0000-0000-000000000066"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000011"), "Amenity", "Name", "sr", null, "Grejanje" },
                    { new Guid("30000000-0000-0000-0000-000000000067"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000012"), "Amenity", "Name", "sq", null, "Ndihma e parë" },
                    { new Guid("30000000-0000-0000-0000-000000000068"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000012"), "Amenity", "Name", "tr", null, "İlk yardım" },
                    { new Guid("30000000-0000-0000-0000-000000000069"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("20000000-0000-0000-0000-000000000012"), "Amenity", "Name", "sr", null, "Prva pomoć" }
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacilitySports_Buffer",
                table: "FacilitySports",
                sql: "\"BufferMinutes\" IS NULL OR \"BufferMinutes\" >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceRules_FacilityId_SportId",
                table: "FacilityPriceRules",
                columns: new[] { "FacilityId", "SportId" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceRules_SportId",
                table: "FacilityPriceRules",
                column: "SportId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacilityPriceRules_Duration",
                table: "FacilityPriceRules",
                sql: "\"DurationMinutes\" IS NULL OR \"DurationMinutes\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacilityPriceRules_Window",
                table: "FacilityPriceRules",
                sql: "(\"StartTime\" IS NULL AND \"EndTime\" IS NULL) OR (\"StartTime\" IS NOT NULL AND \"EndTime\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityOperatingHours_FacilityId_SportId_DayOfWeek",
                table: "FacilityOperatingHours",
                columns: new[] { "FacilityId", "SportId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityOperatingHours_SportId",
                table: "FacilityOperatingHours",
                column: "SportId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FacilityOperatingHours_Window",
                table: "FacilityOperatingHours",
                sql: "\"OpensAt\" <> \"ClosesAt\"");

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_VenueId",
                table: "Facilities",
                column: "VenueId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Advance",
                table: "Facilities",
                sql: "\"MinAdvanceMinutes\" >= 0 AND \"MaxAdvanceDays\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_SlotStep",
                table: "Facilities",
                sql: "\"SlotStepMinutes\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_BusinessId_StartsAt",
                table: "Bookings",
                columns: new[] { "BusinessId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ContactPhone_CreatedAt",
                table: "Bookings",
                columns: new[] { "ContactPhone", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_DeviceHash_CreatedAt",
                table: "Bookings",
                columns: new[] { "DeviceHash", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_FacilityId_StartsAt",
                table: "Bookings",
                columns: new[] { "FacilityId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_GuestAccessTokenHash",
                table: "Bookings",
                column: "GuestAccessTokenHash",
                unique: true,
                filter: "\"GuestAccessTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_IdempotencyKey",
                table: "Bookings",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlocks_SportId",
                table: "AvailabilityBlocks",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                table: "AuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_ActorUserId",
                table: "AuditLogs",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_BusinessId",
                table: "AuditLogs",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_CreatedAt",
                table: "AuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHistory_BookingId_ChangedAt",
                table: "BookingHistory",
                columns: new[] { "BookingId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingHistory_ChangedByUserId",
                table: "BookingHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_OwnerUserId",
                table: "Businesses",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_RegistrationNumber",
                table: "Businesses",
                column: "RegistrationNumber",
                unique: true,
                filter: "\"RegistrationNumber\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Businesses_Status",
                table: "Businesses",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessInvitations_BusinessId_Status",
                table: "BusinessInvitations",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessInvitations_Email",
                table: "BusinessInvitations",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessInvitations_TokenHash",
                table: "BusinessInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMembers_BusinessId_UserId",
                table: "BusinessMembers",
                columns: new[] { "BusinessId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessMembers_UserId",
                table: "BusinessMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSubscriptions_BusinessId_Status",
                table: "BusinessSubscriptions",
                columns: new[] { "BusinessId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSubscriptions_PlanId",
                table: "BusinessSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessSubscriptions_Status_CurrentPeriodEnd",
                table: "BusinessSubscriptions",
                columns: new[] { "Status", "CurrentPeriodEnd" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_ReporterUserId_TargetType_TargetId",
                table: "ContentReports",
                columns: new[] { "ReporterUserId", "TargetType", "TargetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_Status_CreatedAt",
                table: "ContentReports",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ContentReports_TargetType_TargetId",
                table: "ContentReports",
                columns: new[] { "TargetType", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPromotions_FacilityId",
                table: "FacilityPromotions",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPromotions_IsActive_StartsAt_EndsAt",
                table: "FacilityPromotions",
                columns: new[] { "IsActive", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityScheduleExceptions_CreatedByUserId",
                table: "FacilityScheduleExceptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityScheduleExceptions_FacilityId_StartDate_EndDate",
                table: "FacilityScheduleExceptions",
                columns: new[] { "FacilityId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FeatureFlags_Key",
                table: "FeatureFlags",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_BookingId",
                table: "NotificationMessages",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_BusinessId",
                table: "NotificationMessages",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_NotificationMessages_Status_CreatedAt",
                table: "NotificationMessages",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BookingId",
                table: "Payments",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BusinessId_CreatedAt",
                table: "Payments",
                columns: new[] { "BusinessId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_Provider_ProviderReference",
                table: "Payments",
                columns: new[] { "Provider", "ProviderReference" },
                unique: true,
                filter: "\"ProviderReference\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_SubscriptionId",
                table: "Payments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_Phone_CreatedAt",
                table: "PhoneVerifications",
                columns: new[] { "Phone", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PhoneVerifications_VerificationTokenHash",
                table: "PhoneVerifications",
                column: "VerificationTokenHash",
                unique: true,
                filter: "\"VerificationTokenHash\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_BookingId",
                table: "Reviews",
                column: "BookingId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_FacilityId_Status_CreatedAt",
                table: "Reviews",
                columns: new[] { "FacilityId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reviews_UserId",
                table: "Reviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPlans_Name",
                table: "SubscriptionPlans",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers",
                columns: new[] { "TeamId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId",
                table: "TeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CaptainUserId",
                table: "Teams",
                column: "CaptainUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Slug",
                table: "Teams",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SportId_City",
                table: "Teams",
                columns: new[] { "SportId", "City" });

            migrationBuilder.CreateIndex(
                name: "IX_Translations_EntityType_EntityId_Field_Language",
                table: "Translations",
                columns: new[] { "EntityType", "EntityId", "Field", "Language" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Venues_BusinessId",
                table: "Venues",
                column: "BusinessId");

            migrationBuilder.CreateIndex(
                name: "IX_Venues_Slug",
                table: "Venues",
                column: "Slug",
                unique: true);

            // ---------------- HAND EDIT: move existing data to the business model ----------------

            // 1) Businesses keep the ids and details of the old business profiles.
            migrationBuilder.Sql("""
                INSERT INTO "Businesses" ("Id", "OwnerUserId", "BusinessName", "AddressLine", "City", "Country", "Phone", "Email",
                                          "PreferredLanguage", "Status", "CancellationCutoffHours", "CreatedAt", "UpdatedAt")
                SELECT "Id", "OwnerUserId", "BusinessName", "AddressLine", "City", "Country", "Phone", "Email",
                       "PreferredLanguage", 1, 24, "CreatedAt", "UpdatedAt"
                FROM "BusinessProfiles";
                """);

            // 2) Owners with facilities but no saved profile get a placeholder business to complete later.
            //    (Facilities."BusinessId" still holds the owner's user id here: the column was renamed above.)
            migrationBuilder.Sql("""
                INSERT INTO "Businesses" ("Id", "OwnerUserId", "BusinessName", "AddressLine", "City", "Country", "Phone", "Email",
                                          "PreferredLanguage", "Status", "CancellationCutoffHours", "CreatedAt")
                SELECT gen_random_uuid(), u."Id", u."FirstName" || ' (business)', '-', COALESCE(u."City", '-'), 'XK',
                       COALESCE(u."PhoneNumber", '-'), u."Email", 'sq', 1, 24, now()
                FROM "Users" u
                WHERE EXISTS (SELECT 1 FROM "Facilities" f WHERE f."BusinessId" = u."Id")
                  AND NOT EXISTS (SELECT 1 FROM "Businesses" b WHERE b."OwnerUserId" = u."Id");
                """);

            // 3) Every owner becomes the Owner member of their business (255 = all permissions).
            migrationBuilder.Sql("""
                INSERT INTO "BusinessMembers" ("Id", "BusinessId", "UserId", "Role", "Permissions", "IsActive", "CreatedAt")
                SELECT gen_random_uuid(), "Id", "OwnerUserId", 1, 255, true, now() FROM "Businesses";
                """);

            // 4) Facilities point to the owner's business; new rule columns get their defaults.
            migrationBuilder.Sql("""
                UPDATE "Facilities" f SET "BusinessId" = b."Id" FROM "Businesses" b WHERE b."OwnerUserId" = f."BusinessId";
                UPDATE "Facilities" SET "SlotStepMinutes" = 30, "CancellationCutoffHours" = NULL;
                """);

            migrationBuilder.DropTable(
                name: "BusinessProfiles");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilityBlocks_Sports_SportId",
                table: "AvailabilityBlocks",
                column: "SportId",
                principalTable: "Sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Businesses_BusinessId",
                table: "Bookings",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings",
                column: "CreatedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Businesses_BusinessId",
                table: "Facilities",
                column: "BusinessId",
                principalTable: "Businesses",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Venues_VenueId",
                table: "Facilities",
                column: "VenueId",
                principalTable: "Venues",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityOperatingHours_Sports_SportId",
                table: "FacilityOperatingHours",
                column: "SportId",
                principalTable: "Sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityPriceRules_Sports_SportId",
                table: "FacilityPriceRules",
                column: "SportId",
                principalTable: "Sports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            // HAND EDIT: double-booking guard (story 31). Pending (1) and Confirmed (2) reservations of the same
            // facility can't overlap in [StartsAt, BlockedUntil), where BlockedUntil = EndsAt + buffer.
            migrationBuilder.Sql("""
                ALTER TABLE "Bookings" ADD CONSTRAINT "EX_Bookings_NoOverlap"
                EXCLUDE USING gist ("FacilityId" WITH =, tstzrange("StartsAt", "BlockedUntil", '[)') WITH &&)
                WHERE ("Status" IN (1, 2));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // HAND EDIT: Down is lossy (business data model can't be mapped back exactly). Dev use only.
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"EX_Bookings_NoOverlap\";");

            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilityBlocks_Sports_SportId",
                table: "AvailabilityBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Businesses_BusinessId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Users_CreatedByUserId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Businesses_BusinessId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Venues_VenueId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityOperatingHours_Sports_SportId",
                table: "FacilityOperatingHours");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityPriceRules_Sports_SportId",
                table: "FacilityPriceRules");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BookingHistory");

            migrationBuilder.DropTable(
                name: "BusinessInvitations");

            migrationBuilder.DropTable(
                name: "BusinessMembers");

            migrationBuilder.DropTable(
                name: "ContentReports");

            migrationBuilder.DropTable(
                name: "FacilityPromotions");

            migrationBuilder.DropTable(
                name: "FacilityScheduleExceptions");

            migrationBuilder.DropTable(
                name: "FeatureFlags");

            migrationBuilder.DropTable(
                name: "NotificationMessages");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "PhoneVerifications");

            migrationBuilder.DropTable(
                name: "PlatformSettings");

            migrationBuilder.DropTable(
                name: "Reviews");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "Translations");

            migrationBuilder.DropTable(
                name: "Venues");

            migrationBuilder.DropTable(
                name: "BusinessSubscriptions");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Businesses");

            migrationBuilder.DropTable(
                name: "SubscriptionPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacilitySports_Buffer",
                table: "FacilitySports");

            migrationBuilder.DropIndex(
                name: "IX_FacilityPriceRules_FacilityId_SportId",
                table: "FacilityPriceRules");

            migrationBuilder.DropIndex(
                name: "IX_FacilityPriceRules_SportId",
                table: "FacilityPriceRules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacilityPriceRules_Duration",
                table: "FacilityPriceRules");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacilityPriceRules_Window",
                table: "FacilityPriceRules");

            migrationBuilder.DropIndex(
                name: "IX_FacilityOperatingHours_FacilityId_SportId_DayOfWeek",
                table: "FacilityOperatingHours");

            migrationBuilder.DropIndex(
                name: "IX_FacilityOperatingHours_SportId",
                table: "FacilityOperatingHours");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FacilityOperatingHours_Window",
                table: "FacilityOperatingHours");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_VenueId",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Advance",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_SlotStep",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_BusinessId_StartsAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ContactPhone_CreatedAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_DeviceHash_CreatedAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_FacilityId_StartsAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_GuestAccessTokenHash",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_IdempotencyKey",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilityBlocks_SportId",
                table: "AvailabilityBlocks");

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000011"));

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndsAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ProfileVisibility",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                table: "FacilitySports");

            migrationBuilder.DropColumn(
                name: "DurationOptions",
                table: "FacilitySports");

            migrationBuilder.DropColumn(
                name: "DurationMinutes",
                table: "FacilityPriceRules");

            migrationBuilder.DropColumn(
                name: "Label",
                table: "FacilityPriceRules");

            migrationBuilder.DropColumn(
                name: "SportId",
                table: "FacilityPriceRules");

            migrationBuilder.DropColumn(
                name: "SportId",
                table: "FacilityOperatingHours");

            migrationBuilder.DropColumn(
                name: "CancellationPolicyNote",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DurationOptions",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "RequiresOnlinePayment",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "VenueId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "BusinessId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ContactEmail",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ContactName",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ContactPhone",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "DeviceHash",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ExternalChannel",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "GuestAccessTokenHash",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InternalNotes",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IpHash",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "IsGuest",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "InternalNote",
                table: "AvailabilityBlocks");

            migrationBuilder.DropColumn(
                name: "SportId",
                table: "AvailabilityBlocks");

            migrationBuilder.RenameColumn(
                name: "SlotStepMinutes",
                table: "Facilities",
                newName: "ConfirmationMode");

            migrationBuilder.RenameColumn(
                name: "BusinessId",
                table: "Facilities",
                newName: "OwnerUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Facilities_BusinessId_Status",
                table: "Facilities",
                newName: "IX_Facilities_OwnerUserId_Status");

            migrationBuilder.RenameColumn(
                name: "Source",
                table: "Bookings",
                newName: "Spots");

            migrationBuilder.RenameColumn(
                name: "CreatedByUserId",
                table: "Bookings",
                newName: "SessionId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_CreatedByUserId",
                table: "Bookings",
                newName: "IX_Bookings_SessionId");

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "StartTime",
                table: "FacilityPriceRules",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<TimeOnly>(
                name: "EndTime",
                table: "FacilityPriceRules",
                type: "time without time zone",
                nullable: false,
                defaultValue: new TimeOnly(0, 0, 0),
                oldClrType: typeof(TimeOnly),
                oldType: "time without time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidFrom",
                table: "FacilityPriceRules",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ValidTo",
                table: "FacilityPriceRules",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "CancellationCutoffHours",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BookingMode",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DurationStepMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxDurationMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinDurationMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlotDurationMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerUserId",
                table: "Bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfirmationMode",
                table: "Bookings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Reason",
                table: "AvailabilityBlocks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "AvailabilityBlocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "BookingStatusHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FromStatus = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ToStatus = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BookingStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistory_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BookingStatusHistory_Users_ChangedByUserId",
                        column: x => x.ChangedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BusinessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreferredLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessProfiles_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilityMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Permissions = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityMembers_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FacilitySessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SportId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PricePerSpot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilitySessions", x => x.Id);
                    table.CheckConstraint("CK_FacilitySessions_Capacity", "\"Capacity\" > 0 AND \"BookedCount\" >= 0 AND \"BookedCount\" <= \"Capacity\"");
                    table.CheckConstraint("CK_FacilitySessions_Time", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_FacilitySessions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilitySessions_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.UpdateData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"),
                columns: new[] { "Category", "DefaultMaxParticipants", "Name", "Slug" },
                values: new object[] { 5, 4, "Golf", "golf" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceRules_FacilityId_DayOfWeek",
                table: "FacilityPriceRules",
                columns: new[] { "FacilityId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityOperatingHours_FacilityId_DayOfWeek",
                table: "FacilityOperatingHours",
                columns: new[] { "FacilityId", "DayOfWeek" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Durations",
                table: "Facilities",
                sql: "\"MinDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" >= \"MinDurationMinutes\"");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_CustomerUserId_IdempotencyKey",
                table: "Bookings",
                columns: new[] { "CustomerUserId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_FacilityId_Status_StartsAt",
                table: "Bookings",
                columns: new[] { "FacilityId", "Status", "StartsAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Bookings_Spots",
                table: "Bookings",
                sql: "\"Spots\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_BookingId_ChangedAt",
                table: "BookingStatusHistory",
                columns: new[] { "BookingId", "ChangedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_BookingStatusHistory_ChangedByUserId",
                table: "BookingStatusHistory",
                column: "ChangedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessProfiles_OwnerUserId",
                table: "BusinessProfiles",
                column: "OwnerUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityMembers_FacilityId_UserId",
                table: "FacilityMembers",
                columns: new[] { "FacilityId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityMembers_UserId",
                table: "FacilityMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilitySessions_FacilityId_StartsAt",
                table: "FacilitySessions",
                columns: new[] { "FacilityId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilitySessions_SportId",
                table: "FacilitySessions",
                column: "SportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_FacilitySessions_SessionId",
                table: "Bookings",
                column: "SessionId",
                principalTable: "FacilitySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Users_OwnerUserId",
                table: "Facilities",
                column: "OwnerUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
