using Microsoft.EntityFrameworkCore;
using Tdmu.Advisor.Api.Features.KnowledgeBase.Entities;
using Tdmu.Advisor.Api.Infrastructure;

namespace Tdmu.Advisor.Api.Features.KnowledgeBase.Seed;

/// <summary>Dữ liệu mẫu cho dev, chỉ ghi vào bản nháp. Số liệu là giả.</summary>
public static class DevSampleSeeder
{
    public const string Nguon = "DỮ LIỆU MẪU – không phải số liệu chính thức";

    public static async Task SeedAsync(AdvisorDbContext db, CancellationToken ct = default)
    {
        if (await db.Nganh.AnyAsync(n => n.ReleaseId == null, ct)) return;

        const int nam = 2026;
        var mua = new MuaTuyenSinh { Nam = nam, LaHienTai = true, NguonTen = Nguon };
        var nganh = new[]
        {
            new Nganh { MaNganh = "7480201", Ten = "Công nghệ thông tin", DonVi = "Khoa Kỹ thuật – Công nghệ", MoTaNgan = "Mô tả mẫu.", NguonTen = Nguon },
            new Nganh { MaNganh = "7340101", Ten = "Quản trị kinh doanh", DonVi = "Khoa Kinh tế", MoTaNgan = "Mô tả mẫu.", NguonTen = Nguon },
            new Nganh { MaNganh = "7140201", Ten = "Giáo dục mầm non", DonVi = "Khoa Sư phạm", MoTaNgan = "Mô tả mẫu.", NguonTen = Nguon },
        };
        var pt = new[]
        {
            new PhuongThuc { Ma = "thi-thpt", Ten = "thi THPT", ThangDiem = 30m, NguonTen = Nguon },
            new PhuongThuc { Ma = "hoc-ba", Ten = "học bạ", ThangDiem = 30m, NguonTen = Nguon },
            new PhuongThuc { Ma = "dgnl", Ten = "đánh giá năng lực", ThangDiem = 1200m, NguonTen = Nguon },
        };
        var toHop = new[]
        {
            new ToHop { Ma = "A00", Mon = ["Toán", "Vật lý", "Hóa học"], NguonTen = Nguon },
            new ToHop { Ma = "D01", Mon = ["Toán", "Ngữ văn", "Tiếng Anh"], NguonTen = Nguon },
        };
        db.MuaTuyenSinh.Add(mua);
        db.Nganh.AddRange(nganh);
        db.PhuongThuc.AddRange(pt);
        db.ToHop.AddRange(toHop);
        foreach (var n in nganh)
            foreach (var t in toHop)
                db.NganhToHop.Add(new NganhToHop { NganhLid = n.LogicalId, Nam = nam, ToHopLid = t.LogicalId, NguonTen = Nguon });

        decimal[] diem = [20m, 21m, 650m]; // thi THPT, học bạ, ĐGNL (số giả)
        for (var i = 0; i < nganh.Length; i++)
            for (var j = 0; j < pt.Length; j++)
                db.DiemChuan.Add(new DiemChuan
                {
                    NganhLid = nganh[i].LogicalId, Nam = nam, PhuongThucLid = pt[j].LogicalId,
                    Diem = diem[j] + (j == 2 ? i * 10 : i), NguonTen = Nguon,
                });
        await db.SaveChangesAsync(ct);
    }
}
