using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tdmu.Advisor.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HocPhiBusinessKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_hoc_phi_release_id_nganh_lid_nam_don_vi",
                table: "hoc_phi",
                columns: new[] { "release_id", "nganh_lid", "nam", "don_vi" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hoc_phi_release_id_nganh_lid_nam_don_vi",
                table: "hoc_phi");
        }
    }
}
