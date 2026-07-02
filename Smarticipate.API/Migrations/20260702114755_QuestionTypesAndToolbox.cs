using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Smarticipate.API.Migrations
{
    /// <inheritdoc />
    public partial class QuestionTypesAndToolbox : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Responses_Questions_QuestionId",
                table: "Responses");

            migrationBuilder.DropTable(
                name: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Responses_QuestionId",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "QuestionId",
                table: "Responses");

            migrationBuilder.RenameColumn(
                name: "TimeStamp",
                table: "Responses",
                newName: "SubmittedAt");

            migrationBuilder.RenameColumn(
                name: "SelectedOption",
                table: "Responses",
                newName: "ActivationId");

            migrationBuilder.AddColumn<decimal>(
                name: "NumericValue",
                table: "Responses",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParticipantKey",
                table: "Responses",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TextValue",
                table: "Responses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "QuestionDefinitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: true),
                    IsSaved = table.Column<bool>(type: "boolean", nullable: false),
                    OwnerUserId = table.Column<string>(type: "text", nullable: true),
                    ConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionDefinitions_AspNetUsers_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "QuestionActivations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefinitionId = table.Column<int>(type: "integer", nullable: false),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionActivations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionActivations_QuestionDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "QuestionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionActivations_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "QuestionOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DefinitionId = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    IsCorrect = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionOptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionOptions_QuestionDefinitions_DefinitionId",
                        column: x => x.DefinitionId,
                        principalTable: "QuestionDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ResponseSelections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ResponseId = table.Column<int>(type: "integer", nullable: false),
                    OptionId = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResponseSelections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResponseSelections_QuestionOptions_OptionId",
                        column: x => x.OptionId,
                        principalTable: "QuestionOptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ResponseSelections_Responses_ResponseId",
                        column: x => x.ResponseId,
                        principalTable: "Responses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Responses_ActivationId_ParticipantKey",
                table: "Responses",
                columns: new[] { "ActivationId", "ParticipantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionActivations_DefinitionId",
                table: "QuestionActivations",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionActivations_SessionId",
                table: "QuestionActivations",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionDefinitions_OwnerUserId",
                table: "QuestionDefinitions",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionOptions_DefinitionId",
                table: "QuestionOptions",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseSelections_OptionId",
                table: "ResponseSelections",
                column: "OptionId");

            migrationBuilder.CreateIndex(
                name: "IX_ResponseSelections_ResponseId_OptionId",
                table: "ResponseSelections",
                columns: new[] { "ResponseId", "OptionId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Responses_QuestionActivations_ActivationId",
                table: "Responses",
                column: "ActivationId",
                principalTable: "QuestionActivations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Responses_QuestionActivations_ActivationId",
                table: "Responses");

            migrationBuilder.DropTable(
                name: "QuestionActivations");

            migrationBuilder.DropTable(
                name: "ResponseSelections");

            migrationBuilder.DropTable(
                name: "QuestionOptions");

            migrationBuilder.DropTable(
                name: "QuestionDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_Responses_ActivationId_ParticipantKey",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "NumericValue",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "ParticipantKey",
                table: "Responses");

            migrationBuilder.DropColumn(
                name: "TextValue",
                table: "Responses");

            migrationBuilder.RenameColumn(
                name: "SubmittedAt",
                table: "Responses",
                newName: "TimeStamp");

            migrationBuilder.RenameColumn(
                name: "ActivationId",
                table: "Responses",
                newName: "SelectedOption");

            migrationBuilder.AddColumn<int>(
                name: "QuestionId",
                table: "Responses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Questions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SessionId = table.Column<int>(type: "integer", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    QuestionNumber = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Questions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Questions_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Responses_QuestionId",
                table: "Responses",
                column: "QuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_SessionId",
                table: "Questions",
                column: "SessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_Responses_Questions_QuestionId",
                table: "Responses",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
