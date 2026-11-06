namespace Tdmu.Advisor.Api.Infrastructure.Storage;

/// <summary>Nơi lưu file tài liệu tải lên. Đường dẫn trả về là đường dẫn tương đối so với thư mục gốc lưu trữ.</summary>
public interface IFileStore
{
    /// <summary>Lưu nội dung và trả đường dẫn tương đối <c>"{logicalId}{ext}"</c>.</summary>
    Task<string> SaveAsync(Guid logicalId, string ext, Stream content, CancellationToken ct);
    Task<byte[]> ReadAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, CancellationToken ct);
}
