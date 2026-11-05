using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace Tdmu.Advisor.Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KnowledgeBase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entity = table.Column<string>(type: "text", nullable: false),
                    entity_logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    before_json = table.Column<string>(type: "jsonb", nullable: true),
                    after_json = table.Column<string>(type: "jsonb", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "release",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    so_hieu = table.Column<int>(type: "integer", nullable: false),
                    ghi_chu = table.Column<string>(type: "text", nullable: false),
                    tao_boi = table.Column<string>(type: "text", nullable: false),
                    tao_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_release", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "chi_tieu",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nganh_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    phuong_thuc_lid = table.Column<Guid>(type: "uuid", nullable: true),
                    so_luong = table.Column<int>(type: "integer", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chi_tieu", x => x.id);
                    table.ForeignKey(
                        name: "fk_chi_tieu_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "diem_chuan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nganh_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    phuong_thuc_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    diem = table.Column<decimal>(type: "numeric(7,2)", nullable: false),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_diem_chuan", x => x.id);
                    table.ForeignKey(
                        name: "fk_diem_chuan_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faq",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    cau_hoi = table.Column<string>(type: "text", nullable: false),
                    cach_hoi_khac = table.Column<List<string>>(type: "text[]", nullable: false),
                    tra_loi = table.Column<string>(type: "text", nullable: false),
                    chu_de = table.Column<string>(type: "text", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_faq", x => x.id);
                    table.ForeignKey(
                        name: "fk_faq_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hoc_phi",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nganh_lid = table.Column<Guid>(type: "uuid", nullable: true),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    muc = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    don_vi = table.Column<string>(type: "text", nullable: false),
                    ghi_chu = table.Column<string>(type: "text", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hoc_phi", x => x.id);
                    table.ForeignKey(
                        name: "fk_hoc_phi_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moc_thoi_gian",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    ten_moc = table.Column<string>(type: "text", nullable: false),
                    bat_dau = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ket_thuc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    phuong_thuc_lid = table.Column<Guid>(type: "uuid", nullable: true),
                    mo_ta = table.Column<string>(type: "text", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moc_thoi_gian", x => x.id);
                    table.ForeignKey(
                        name: "fk_moc_thoi_gian_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mua_tuyen_sinh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    la_hien_tai = table.Column<bool>(type: "boolean", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mua_tuyen_sinh", x => x.id);
                    table.ForeignKey(
                        name: "fk_mua_tuyen_sinh_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nganh",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ma_nganh = table.Column<string>(type: "text", nullable: false),
                    ten = table.Column<string>(type: "text", nullable: false),
                    ten_goi_khac = table.Column<List<string>>(type: "text[]", nullable: false),
                    don_vi = table.Column<string>(type: "text", nullable: true),
                    mo_ta_ngan = table.Column<string>(type: "text", nullable: true),
                    con_tuyen = table.Column<bool>(type: "boolean", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nganh", x => x.id);
                    table.ForeignKey(
                        name: "fk_nganh_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "nganh_to_hop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nganh_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    nam = table.Column<int>(type: "integer", nullable: false),
                    to_hop_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nganh_to_hop", x => x.id);
                    table.ForeignKey(
                        name: "fk_nganh_to_hop_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "phuong_thuc",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ma = table.Column<string>(type: "text", nullable: false),
                    ten = table.Column<string>(type: "text", nullable: false),
                    mo_ta = table.Column<string>(type: "text", nullable: true),
                    thang_diem = table.Column<decimal>(type: "numeric(7,2)", nullable: false, defaultValue: 30m),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phuong_thuc", x => x.id);
                    table.ForeignKey(
                        name: "fk_phuong_thuc_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tai_lieu",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tieu_de = table.Column<string>(type: "text", nullable: false),
                    duong_dan_file = table.Column<string>(type: "text", nullable: false),
                    nam_ap_dung = table.Column<int>(type: "integer", nullable: true),
                    trang_thai = table.Column<string>(type: "text", nullable: false),
                    loi = table.Column<string>(type: "text", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tai_lieu", x => x.id);
                    table.ForeignKey(
                        name: "fk_tai_lieu_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tai_lieu_chunk",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tai_lieu_lid = table.Column<Guid>(type: "uuid", nullable: false),
                    thu_tu = table.Column<int>(type: "integer", nullable: false),
                    noi_dung = table.Column<string>(type: "text", nullable: false),
                    vi_tri = table.Column<string>(type: "text", nullable: true),
                    embedding = table.Column<Vector>(type: "vector(768)", nullable: true),
                    tsv = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tai_lieu_chunk", x => x.id);
                    table.ForeignKey(
                        name: "fk_tai_lieu_chunk_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "to_hop",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ma = table.Column<string>(type: "text", nullable: false),
                    mon = table.Column<List<string>>(type: "text[]", nullable: false),
                    cach_doc = table.Column<string>(type: "text", nullable: true),
                    logical_id = table.Column<Guid>(type: "uuid", nullable: false),
                    release_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nguon_ten = table.Column<string>(type: "text", nullable: true),
                    nguon_url = table.Column<string>(type: "text", nullable: true),
                    cap_nhat_luc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_to_hop", x => x.id);
                    table.ForeignKey(
                        name: "fk_to_hop_release_release_id",
                        column: x => x.release_id,
                        principalTable: "release",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entity_entity_logical_id",
                table: "audit_log",
                columns: new[] { "entity", "entity_logical_id" });

            migrationBuilder.CreateIndex(
                name: "ix_chi_tieu_release_id_logical_id",
                table: "chi_tieu",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_chi_tieu_release_id_nganh_lid_nam_phuong_thuc_lid",
                table: "chi_tieu",
                columns: new[] { "release_id", "nganh_lid", "nam", "phuong_thuc_lid" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_diem_chuan_release_id_logical_id",
                table: "diem_chuan",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_diem_chuan_release_id_nganh_lid_nam_phuong_thuc_lid",
                table: "diem_chuan",
                columns: new[] { "release_id", "nganh_lid", "nam", "phuong_thuc_lid" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_faq_embedding",
                table: "faq",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_faq_release_id_logical_id",
                table: "faq",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_hoc_phi_release_id_logical_id",
                table: "hoc_phi",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_moc_thoi_gian_release_id_logical_id",
                table: "moc_thoi_gian",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_mua_tuyen_sinh_release_id",
                table: "mua_tuyen_sinh",
                column: "release_id",
                unique: true,
                filter: "la_hien_tai")
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_mua_tuyen_sinh_release_id_logical_id",
                table: "mua_tuyen_sinh",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_nganh_release_id_logical_id",
                table: "nganh",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_nganh_release_id_ma_nganh",
                table: "nganh",
                columns: new[] { "release_id", "ma_nganh" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_nganh_ten",
                table: "nganh",
                column: "ten")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_nganh_to_hop_release_id_logical_id",
                table: "nganh_to_hop",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_nganh_to_hop_release_id_nganh_lid_nam_to_hop_lid",
                table: "nganh_to_hop",
                columns: new[] { "release_id", "nganh_lid", "nam", "to_hop_lid" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_phuong_thuc_release_id_logical_id",
                table: "phuong_thuc",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_phuong_thuc_release_id_ma",
                table: "phuong_thuc",
                columns: new[] { "release_id", "ma" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_release_is_active",
                table: "release",
                column: "is_active",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_release_id_logical_id",
                table: "tai_lieu",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_chunk_embedding",
                table: "tai_lieu_chunk",
                column: "embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_chunk_release_id_logical_id",
                table: "tai_lieu_chunk",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_tai_lieu_chunk_tsv",
                table: "tai_lieu_chunk",
                column: "tsv")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_to_hop_release_id_logical_id",
                table: "to_hop",
                columns: new[] { "release_id", "logical_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_to_hop_release_id_ma",
                table: "to_hop",
                columns: new[] { "release_id", "ma" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log");

            migrationBuilder.DropTable(
                name: "chi_tieu");

            migrationBuilder.DropTable(
                name: "diem_chuan");

            migrationBuilder.DropTable(
                name: "faq");

            migrationBuilder.DropTable(
                name: "hoc_phi");

            migrationBuilder.DropTable(
                name: "moc_thoi_gian");

            migrationBuilder.DropTable(
                name: "mua_tuyen_sinh");

            migrationBuilder.DropTable(
                name: "nganh");

            migrationBuilder.DropTable(
                name: "nganh_to_hop");

            migrationBuilder.DropTable(
                name: "phuong_thuc");

            migrationBuilder.DropTable(
                name: "tai_lieu");

            migrationBuilder.DropTable(
                name: "tai_lieu_chunk");

            migrationBuilder.DropTable(
                name: "to_hop");

            migrationBuilder.DropTable(
                name: "release");
        }
    }
}
