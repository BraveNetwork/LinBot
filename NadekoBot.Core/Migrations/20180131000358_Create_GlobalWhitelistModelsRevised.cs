using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace NadekoBot.Migrations
{
    public partial class Create_GlobalWhitelistModelsRevised : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GlobalWhitelistItem",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    ItemId = table.Column<ulong>(nullable: false),
                    Type = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalWhitelistItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalWhitelistItem_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GlobalWhitelistSet",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    IsEnabled = table.Column<bool>(nullable: false, defaultValue: true),
                    ListName = table.Column<string>(maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalWhitelistSet", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GlobalWhitelistSet_BotConfig_BotConfigId",
                        column: x => x.BotConfigId,
                        principalTable: "BotConfig",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UnblockedCmdOrMdl",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BotConfigId = table.Column<int>(nullable: true),
                    BotConfigId1 = table.Column<int>(nullable: true),
                    DateAdded = table.Column<DateTime>(nullable: true),
                    Name = table.Column<string>(nullable: true),
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
                name: "GlobalWhitelistItemSet",
                columns: table => new
                {
                    ListPK = table.Column<int>(nullable: false),
                    ItemPK = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalWhitelistItemSet", x => new { x.ListPK, x.ItemPK });
                    table.ForeignKey(
                        name: "FK_GlobalWhitelistItemSet_GlobalWhitelistItem_ItemPK",
                        column: x => x.ItemPK,
                        principalTable: "GlobalWhitelistItem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GlobalWhitelistItemSet_GlobalWhitelistSet_ListPK",
                        column: x => x.ListPK,
                        principalTable: "GlobalWhitelistSet",
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
                        name: "FK_GlobalUnblockedSet_GlobalWhitelistSet_ListPK",
                        column: x => x.ListPK,
                        principalTable: "GlobalWhitelistSet",
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
                name: "IX_GlobalWhitelistItem_BotConfigId",
                table: "GlobalWhitelistItem",
                column: "BotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalWhitelistItemSet_ItemPK",
                table: "GlobalWhitelistItemSet",
                column: "ItemPK");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalWhitelistSet_BotConfigId",
                table: "GlobalWhitelistSet",
                column: "BotConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalWhitelistSet_ListName",
                table: "GlobalWhitelistSet",
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
                name: "GlobalWhitelistItemSet");

            migrationBuilder.DropTable(
                name: "UnblockedCmdOrMdl");

            migrationBuilder.DropTable(
                name: "GlobalWhitelistItem");

            migrationBuilder.DropTable(
                name: "GlobalWhitelistSet");
        }
    }
}
