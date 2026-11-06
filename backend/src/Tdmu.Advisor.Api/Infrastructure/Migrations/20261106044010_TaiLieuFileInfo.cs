using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tdmu.Advisor.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TaiLieuFileInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "kich_thuoc",
                table: "tai_lieu",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<string>(
                name: "loai_noi_dung",
                table: "tai_lieu",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ten_file_goc",
                table: "tai_lieu",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "kich_thuoc",
                table: "tai_lieu");

            migrationBuilder.DropColumn(
                name: "loai_noi_dung",
                table: "tai_lieu");

            migrationBuilder.DropColumn(
                name: "ten_file_goc",
                table: "tai_lieu");
        }
    }
}
