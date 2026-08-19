using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace PollBuilder.Voting.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialVotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "voting");

            migrationBuilder.CreateTable(
                name: "Votes",
                schema: "voting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PollCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    OptionIndex = table.Column<int>(type: "integer", nullable: false),
                    VoterToken = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VotedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_OptionIndex",
                schema: "voting",
                table: "Votes",
                columns: new[] { "PollCode", "OptionIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Votes_PollCode_VoterToken",
                schema: "voting",
                table: "Votes",
                columns: new[] { "PollCode", "VoterToken" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Votes",
                schema: "voting");
        }
    }
}
