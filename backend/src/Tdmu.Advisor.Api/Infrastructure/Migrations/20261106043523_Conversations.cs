using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tdmu.Advisor.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Conversations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "conversation",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kenh_dau_tien = table.Column<string>(type: "text", nullable: false),
                    bat_dau = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_agent = table.Column<string>(type: "text", nullable: true),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    la_hoi_thu = table.Column<bool>(type: "boolean", nullable: false),
                    dong_y_giong_noi = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "message",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    vai_tro = table.Column<string>(type: "text", nullable: false),
                    kenh = table.Column<string>(type: "text", nullable: false),
                    noi_dung = table.Column<string>(type: "text", nullable: false),
                    tool_calls = table.Column<string>(type: "jsonb", nullable: true),
                    latency_ms = table.Column<string>(type: "jsonb", nullable: true),
                    danh_gia = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    co_loi = table.Column<bool>(type: "boolean", nullable: false),
                    tao_luc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_message", x => x.id);
                    table.ForeignKey(
                        name: "fk_message_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ticket",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    ho_ten = table.Column<string>(type: "text", nullable: false),
                    lien_he = table.Column<string>(type: "text", nullable: false),
                    ly_do = table.Column<string>(type: "text", nullable: false),
                    trang_thai = table.Column<string>(type: "text", nullable: false, defaultValue: "moi"),
                    tao_luc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ticket", x => x.id);
                    table.ForeignKey(
                        name: "fk_ticket_conversation_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_message_conversation_id_tao_luc",
                table: "message",
                columns: new[] { "conversation_id", "tao_luc" });

            migrationBuilder.CreateIndex(
                name: "ix_ticket_conversation_id",
                table: "ticket",
                column: "conversation_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "message");

            migrationBuilder.DropTable(
                name: "ticket");

            migrationBuilder.DropTable(
                name: "conversation");
        }
    }
}
