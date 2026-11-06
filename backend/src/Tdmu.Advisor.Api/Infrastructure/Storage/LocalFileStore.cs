using Microsoft.Extensions.Hosting;

namespace Tdmu.Advisor.Api.Infrastructure.Storage;

/// <summary>Lưu file trên đĩa dưới <c>Storage:Root</c> (mặc định <c>data/uploads</c>, tính từ thư mục nội dung của ứng dụng).</summary>
public class LocalFileStore(IConfiguration config, IHostEnvironment env) : IFileStore
{
    private readonly string _root = Path.GetFullPath(config["Storage:Root"] ?? "data/uploads", env.ContentRootPath);

    public async Task<string> SaveAsync(Guid logicalId, string ext, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(_root);
        var rel = $"{logicalId}{ext}";
        await using var fs = new FileStream(Resolve(rel), FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);
        return rel;
    }

    public Task<byte[]> ReadAsync(string path, CancellationToken ct) => File.ReadAllBytesAsync(Resolve(path), ct);

    public Task DeleteAsync(string path, CancellationToken ct)
    {
        var full = Resolve(path);
        if (File.Exists(full)) File.Delete(full);
        return Task.CompletedTask;
    }

    /// <summary>Chặn đường dẫn thoát ra ngoài thư mục gốc.</summary>
    private string Resolve(string rel)
    {
        var full = Path.GetFullPath(Path.Combine(_root, rel));
        if (!full.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Đường dẫn file không hợp lệ", nameof(rel));
        return full;
    }
}
