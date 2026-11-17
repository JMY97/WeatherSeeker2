using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace WebApp1.Migrations
{
    /// <summary>
    /// Collapses Admins/Clients/Users into a single Accounts table and drops the
    /// admin role. Existing rows are copied across first so a live deployment
    /// keeps its users; only the duplicate admin/client split is discarded.
    /// </summary>
    public partial class ConsolidateAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Accounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SessionVersion = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000"))
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Accounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_Username",
                table: "Accounts",
                column: "Username",
                unique: true);

            // An external audit registered two probe accounts and then published
            // their usernames and passwords in the report. Migrating them would
            // hand anyone who has read that report a working login, so they are
            // removed here rather than carried across. Exact names, not a
            // pattern: these are the only credentials known to be compromised.
            migrationBuilder.Sql(
                """
                DELETE FROM "Clients"
                WHERE lower(btrim("username")) IN ('audit_probe_9f2c', 'audit_probe_trim');
                """);

            // Carry every existing credential into Accounts before the old tables
            // go. Usernames are lowercased because that is how sign-up and sign-in
            // now compare them, and a name present in both old tables would
            // otherwise break the unique index: the client row wins.
            //
            // Hashes are portable: PasswordHasher<T> ignores TUser, so a hash made
            // with PasswordHasher<string> still verifies here. A legacy plaintext
            // row is copied unchanged and simply fails to authenticate.
            migrationBuilder.Sql(
                """
                INSERT INTO "Accounts" ("Username", "PasswordHash", "CreatedAt", "SessionVersion")
                SELECT dedup."Username", dedup."PasswordHash", now(), gen_random_uuid()
                FROM (
                    SELECT DISTINCT ON (lower(btrim(legacy."username")))
                           lower(btrim(legacy."username")) AS "Username",
                           legacy."password" AS "PasswordHash"
                    FROM (
                        SELECT "username", "password", 0 AS priority FROM "Clients"
                        UNION ALL
                        SELECT "username", "password", 1 AS priority FROM "Admins"
                    ) AS legacy
                    WHERE btrim(legacy."username") <> ''
                    ORDER BY lower(btrim(legacy."username")), legacy.priority
                ) AS dedup
                ON CONFLICT ("Username") DO NOTHING;
                """);

            migrationBuilder.DropTable(
                name: "Admins");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Admins",
                columns: table => new
                {
                    AdminId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    password = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admins", x => x.AdminId);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    ClientId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    password = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => x.ClientId);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admins_username",
                table: "Admins",
                column: "username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clients_username",
                table: "Clients",
                column: "username",
                unique: true);

            // Every account comes back as a client: the admin role no longer
            // exists, so there is no correct way to restore that distinction.
            // Ids are carried over explicitly so the Clients.Id -> Users.Id link
            // still means what it used to.
            migrationBuilder.Sql(
                """
                INSERT INTO "Users" ("Id", "name") OVERRIDING SYSTEM VALUE
                SELECT a."Id", a."Username" FROM "Accounts" AS a
                ORDER BY a."Id";

                INSERT INTO "Clients" ("Id", "username", "password") OVERRIDING SYSTEM VALUE
                SELECT a."Id", a."Username", a."PasswordHash" FROM "Accounts" AS a
                ORDER BY a."Id";

                SELECT setval(pg_get_serial_sequence('"Users"', 'Id'), COALESCE(max("Id"), 1)) FROM "Users";
                SELECT setval(pg_get_serial_sequence('"Clients"', 'ClientId'), COALESCE(max("ClientId"), 1)) FROM "Clients";
                """);
        }
    }
}
