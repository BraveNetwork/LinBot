using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace NadekoBot.Migrations
{
    public partial class gwlglobalunblock : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GWLItem",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValueSql: "datetime('now')"),
                    ItemId = table.Column<ulong>(nullable: false),
                    RoleServerId = table.Column<ulong>(nullable: false, defaultValue: 0ul),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GWLItem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GWLSet",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValueSql: "datetime('now')"),
                    IsEnabled = table.Column<bool>(nullable: false, defaultValue: true),
                    ListName = table.Column<string>(maxLength: 20, nullable: false),
                    Type = table.Column<int>(nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GWLSet", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnblockedCmdOrMdl",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    BotConfigId1 = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true, defaultValueSql: "datetime('now')"),
                    Name = table.Column<string>(nullable: false),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnblockedCmdOrMdl", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnblockedCmdOrMdl_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UnblockedCmdOrMdl_BotConfig_BotConfigId1",
                        column: x => x.BotConfigId1,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GWLItemSet",
                columns: table => new
                {
                    ListPK = table.Column<int>(nullable: false),
                    ItemPK = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GWLItemSet", x => new { x.ListPK, x.ItemPK });
                    table.ForeignKey(
                        name: "FK_GWLItemSet_GWLItem_ItemPK",
                        column: x => x.ItemPK,
                        principalTable: "GWLItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GWLItemSet_GWLSet_ListPK",
                        column: x => x.ListPK,
                        principalTable: "GWLSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GlobalUnblockedSet",
                columns: table => new
                {
                    ListPK = table.Column<int>(nullable: false),
                    UnblockedPK = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalUnblockedSet", x => new { x.ListPK, x.UnblockedPK });
                    table.ForeignKey(
                        name: "FK_GlobalUnblockedSet_GWLSet_ListPK",
                        column: x => x.ListPK,
                        principalTable: "GWLSet",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GlobalUnblockedSet_UnblockedCmdOrMdl_UnblockedPK",
                        column: x => x.UnblockedPK,
                        principalTable: "UnblockedCmdOrMdl",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalUnblockedSet_UnblockedPK",
                table: "GlobalUnblockedSet",
                column: "UnblockedPK");

            migrationBuilder.CreateIndex(
                name: "IX_GWLItemSet_ItemPK",
                table: "GWLItemSet",
                column: "ItemPK");

            migrationBuilder.CreateIndex(
                name: "IX_GWLSet_ListName",
                table: "GWLSet",
                column: "ListName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnblockedCmdOrMdl_BotConfigId",
                table: "UnblockedCmdOrMdl",
                column: "BotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_UnblockedCmdOrMdl_BotConfigId1",
                table: "UnblockedCmdOrMdl",
                column: "BotConfigId1");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GlobalUnblockedSet");

            migrationBuilder.DropTable(
                name: "GWLItemSet");

            migrationBuilder.DropTable(
                name: "UnblockedCmdOrMdl");

            migrationBuilder.DropTable(
                name: "GWLItem");

            migrationBuilder.DropTable(
                name: "GWLSet");
        }
    }
}
