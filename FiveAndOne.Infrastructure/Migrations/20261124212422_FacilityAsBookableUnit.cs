using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FiveAndOne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FacilityAsBookableUnit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The no-overlap constraint was on ResourceId, which is removed below. Recreated on FacilityId at the end.
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"EX_Bookings_NoOverlap\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Activities_Resources_ResourceId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilityBlocks_Resources_ResourceId",
                table: "AvailabilityBlocks");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_ResourceSessions_SessionId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Resources_ResourceId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Users_ReviewedByUserId",
                table: "Facilities");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityAmenities_Amenities_AmenityId",
                table: "FacilityAmenities");

            migrationBuilder.DropTable(
                name: "ResourcePriceRules");

            migrationBuilder.DropTable(
                name: "ResourceSchedules");

            migrationBuilder.DropTable(
                name: "ResourceSessions");

            migrationBuilder.DropTable(
                name: "ResourceSports");

            migrationBuilder.DropTable(
                name: "Resources");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_OwnerUserId",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_ReviewedByUserId",
                table: "Facilities");

            migrationBuilder.DropIndex(
                name: "IX_Bookings_ResourceId_StartsAt",
                table: "Bookings");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilityBlocks_ResourceId_StartsAt_EndsAt",
                table: "AvailabilityBlocks");

            migrationBuilder.DropIndex(
                name: "IX_Activities_ResourceId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Website",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "Bookings");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "AvailabilityBlocks");

            migrationBuilder.DropColumn(
                name: "ResourceId",
                table: "Activities");

            migrationBuilder.RenameColumn(
                name: "ReviewedAt",
                table: "Facilities",
                newName: "SuspendedAt");

            migrationBuilder.RenameColumn(
                name: "RejectionReason",
                table: "Facilities",
                newName: "SuspensionReason");

            // Hand-fixed: EF guessed DefaultConfirmationMode -> MinAdvanceMinutes. The real rename is -> ConfirmationMode.
            migrationBuilder.RenameColumn(
                name: "DefaultConfirmationMode",
                table: "Facilities",
                newName: "ConfirmationMode");

            migrationBuilder.AddColumn<int>(
                name: "MinAdvanceMinutes",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BookingMode",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "BufferMinutes",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CancellationCutoffHours",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Capacity",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "Facilities",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "DurationStepMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsIndoor",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MaxAdvanceDays",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

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

            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAt",
                table: "Facilities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SlotDurationMinutes",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Surface",
                table: "Facilities",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                table: "Facilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Facilities",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "FacilityPriceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    PriceType = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityPriceRules", x => x.Id);
                    table.CheckConstraint("CK_FacilityPriceRules_Amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_FacilityPriceRules_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
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
                    StartsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: false),
                    BookedCount = table.Column<int>(type: "integer", nullable: false),
                    PricePerSpot = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "FacilitySports",
                columns: table => new
                {
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    SportId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilitySports", x => new { x.FacilityId, x.SportId });
                    table.ForeignKey(
                        name: "FK_FacilitySports_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilitySports_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Amenities",
                columns: new[] { "Id", "CreatedAt", "Icon", "Name", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "parking", "Parking", null },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "changing-room", "Changing rooms", null },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "shower", "Showers", null },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "locker", "Lockers", null },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "floodlight", "Floodlights", null },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "cafe", "Café / Bar", null },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "equipment", "Equipment rental", null },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "seating", "Spectator seating", null },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "wifi", "Wi-Fi", null },
                    { new Guid("20000000-0000-0000-0000-000000000010"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "air-conditioning", "Air conditioning", null },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "heating", "Heating", null },
                    { new Guid("20000000-0000-0000-0000-000000000012"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "first-aid", "First aid", null }
                });

            migrationBuilder.InsertData(
                table: "Sports",
                columns: new[] { "Id", "Category", "CreatedAt", "DefaultMaxParticipants", "DefaultMinParticipants", "Description", "IconUrl", "IsActive", "MetadataJson", "Name", "Slug", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 22, 10, null, null, true, null, "Football", "football", 1, null },
                    { new Guid("10000000-0000-0000-0000-000000000002"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 12, 6, null, null, true, null, "Futsal", "futsal", 2, null },
                    { new Guid("10000000-0000-0000-0000-000000000003"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10, 2, null, null, true, null, "Basketball", "basketball", 3, null },
                    { new Guid("10000000-0000-0000-0000-000000000004"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 2, null, null, true, null, "Tennis", "tennis", 4, null },
                    { new Guid("10000000-0000-0000-0000-000000000005"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 2, null, null, true, null, "Padel", "padel", 5, null },
                    { new Guid("10000000-0000-0000-0000-000000000006"), 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 1, null, null, true, null, "Golf", "golf", 6, null },
                    { new Guid("10000000-0000-0000-0000-000000000007"), 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 12, 4, null, null, true, null, "Volleyball", "volleyball", 7, null },
                    { new Guid("10000000-0000-0000-0000-000000000008"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 2, null, null, true, null, "Badminton", "badminton", 8, null },
                    { new Guid("10000000-0000-0000-0000-000000000009"), 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 10, 1, null, null, true, null, "Swimming", "swimming", 9, null },
                    { new Guid("10000000-0000-0000-0000-000000000010"), 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 4, 2, null, null, true, null, "Table Tennis", "table-tennis", 10, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_OwnerUserId_Status",
                table: "Facilities",
                columns: new[] { "OwnerUserId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Buffer",
                table: "Facilities",
                sql: "\"BufferMinutes\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Capacity",
                table: "Facilities",
                sql: "\"Capacity\" IS NULL OR \"Capacity\" > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Facilities_Durations",
                table: "Facilities",
                sql: "\"MinDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" >= \"MinDurationMinutes\"");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityPriceRules_FacilityId_DayOfWeek",
                table: "FacilityPriceRules",
                columns: new[] { "FacilityId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilitySessions_FacilityId_StartsAt",
                table: "FacilitySessions",
                columns: new[] { "FacilityId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_FacilitySessions_SportId",
                table: "FacilitySessions",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilitySports_SportId",
                table: "FacilitySports",
                column: "SportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_FacilitySessions_SessionId",
                table: "Bookings",
                column: "SessionId",
                principalTable: "FacilitySessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityAmenities_Amenities_AmenityId",
                table: "FacilityAmenities",
                column: "AmenityId",
                principalTable: "Amenities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Double-booking protection, now per facility (each facility is one bookable unit).
            // Pending (1) / Confirmed (2) bookings on the same facility can't overlap; back-to-back is fine.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Bookings"
                ADD CONSTRAINT "EX_Bookings_NoOverlap"
                EXCLUDE USING gist (
                    "FacilityId" WITH =,
                    tstzrange("StartsAt", "BlockedUntil", '[)') WITH &&
                )
                WHERE ("Status" IN (1, 2) AND "SessionId" IS NULL);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE \"Bookings\" DROP CONSTRAINT IF EXISTS \"EX_Bookings_NoOverlap\";");

            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_FacilitySessions_SessionId",
                table: "Bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_FacilityAmenities_Amenities_AmenityId",
                table: "FacilityAmenities");

            migrationBuilder.DropTable(
                name: "FacilityPriceRules");

            migrationBuilder.DropTable(
                name: "FacilitySessions");

            migrationBuilder.DropTable(
                name: "FacilitySports");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_OwnerUserId_Status",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Buffer",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Capacity",
                table: "Facilities");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Facilities_Durations",
                table: "Facilities");

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "Amenities",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000007"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "Sports",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000010"));

            migrationBuilder.DropColumn(
                name: "BookingMode",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "BufferMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "CancellationCutoffHours",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Capacity",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MinAdvanceMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "DurationStepMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "IsIndoor",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MaxAdvanceDays",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MaxDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "MinDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "PublishedAt",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "SlotDurationMinutes",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Surface",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Facilities");

            migrationBuilder.RenameColumn(
                name: "SuspensionReason",
                table: "Facilities",
                newName: "RejectionReason");

            migrationBuilder.RenameColumn(
                name: "SuspendedAt",
                table: "Facilities",
                newName: "ReviewedAt");

            migrationBuilder.RenameColumn(
                name: "ConfirmationMode",
                table: "Facilities",
                newName: "DefaultConfirmationMode");

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Facilities",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "Facilities",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "Facilities",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Website",
                table: "Facilities",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "Bookings",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "AvailabilityBlocks",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResourceId",
                table: "Activities",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Resources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingMode = table.Column<int>(type: "integer", nullable: false),
                    BufferMinutes = table.Column<int>(type: "integer", nullable: false),
                    CancellationCutoffHours = table.Column<int>(type: "integer", nullable: false),
                    Capacity = table.Column<int>(type: "integer", nullable: true),
                    ConfirmationMode = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DurationStepMinutes = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsIndoor = table.Column<bool>(type: "boolean", nullable: false),
                    MaxAdvanceDays = table.Column<int>(type: "integer", nullable: false),
                    MaxDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    MinAdvanceMinutes = table.Column<int>(type: "integer", nullable: false),
                    MinDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<int>(type: "integer", nullable: false),
                    SlotDurationMinutes = table.Column<int>(type: "integer", nullable: true),
                    Surface = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources", x => x.Id);
                    table.CheckConstraint("CK_Resources_Buffer", "\"BufferMinutes\" >= 0");
                    table.CheckConstraint("CK_Resources_Durations", "\"MinDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" IS NULL OR \"MaxDurationMinutes\" >= \"MinDurationMinutes\"");
                    table.ForeignKey(
                        name: "FK_Resources_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourcePriceRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: true),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    PriceType = table.Column<int>(type: "integer", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourcePriceRules", x => x.Id);
                    table.CheckConstraint("CK_ResourcePriceRules_Amount", "\"Amount\" >= 0");
                    table.ForeignKey(
                        name: "FK_ResourcePriceRules_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResourceSchedules_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_ResourceSessions", x => x.Id);
                    table.CheckConstraint("CK_ResourceSessions_Capacity", "\"Capacity\" > 0 AND \"BookedCount\" >= 0 AND \"BookedCount\" <= \"Capacity\"");
                    table.CheckConstraint("CK_ResourceSessions_Time", "\"EndsAt\" > \"StartsAt\"");
                    table.ForeignKey(
                        name: "FK_ResourceSessions_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResourceSessions_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResourceSports",
                columns: table => new
                {
                    ResourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SportId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResourceSports", x => new { x.ResourceId, x.SportId });
                    table.ForeignKey(
                        name: "FK_ResourceSports_Resources_ResourceId",
                        column: x => x.ResourceId,
                        principalTable: "Resources",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ResourceSports_Sports_SportId",
                        column: x => x.SportId,
                        principalTable: "Sports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_OwnerUserId",
                table: "Facilities",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_ReviewedByUserId",
                table: "Facilities",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Bookings_ResourceId_StartsAt",
                table: "Bookings",
                columns: new[] { "ResourceId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilityBlocks_ResourceId_StartsAt_EndsAt",
                table: "AvailabilityBlocks",
                columns: new[] { "ResourceId", "StartsAt", "EndsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_ResourceId",
                table: "Activities",
                column: "ResourceId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourcePriceRules_ResourceId_DayOfWeek",
                table: "ResourcePriceRules",
                columns: new[] { "ResourceId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_Resources_FacilityId_IsActive",
                table: "Resources",
                columns: new[] { "FacilityId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSchedules_ResourceId_DayOfWeek",
                table: "ResourceSchedules",
                columns: new[] { "ResourceId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSessions_ResourceId_StartsAt",
                table: "ResourceSessions",
                columns: new[] { "ResourceId", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSessions_SportId",
                table: "ResourceSessions",
                column: "SportId");

            migrationBuilder.CreateIndex(
                name: "IX_ResourceSports_SportId",
                table: "ResourceSports",
                column: "SportId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_Resources_ResourceId",
                table: "Activities",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilityBlocks_Resources_ResourceId",
                table: "AvailabilityBlocks",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_ResourceSessions_SessionId",
                table: "Bookings",
                column: "SessionId",
                principalTable: "ResourceSessions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Resources_ResourceId",
                table: "Bookings",
                column: "ResourceId",
                principalTable: "Resources",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Users_ReviewedByUserId",
                table: "Facilities",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FacilityAmenities_Amenities_AmenityId",
                table: "FacilityAmenities",
                column: "AmenityId",
                principalTable: "Amenities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
