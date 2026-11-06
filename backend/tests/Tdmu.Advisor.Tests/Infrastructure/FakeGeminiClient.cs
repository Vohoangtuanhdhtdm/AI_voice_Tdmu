using System.Security.Cryptography;
using System.Text;
using Tdmu.Advisor.Api.Infrastructure.Gemini;

namespace Tdmu.Advisor.Tests.Infrastructure;

/// <summary>Đặt vào một lượt của <see cref="FakeGeminiClient.Script"/>: tới chunk này thì ném lỗi (lỗi giữa chừng khi stream).</summary>
public record ThrowChunk(Exception Exception) : GeminiChunk([], null);

/// <summary>Gemini giả cho test: embedding tất định, lượt stream soạn sẵn, không gọi mạng.</summary>
public class FakeGeminiClient : IGeminiClient
{
    public const int Dimensions = 768;

    public Func<string, float[]>? EmbedOverride { get; set; }
    private int _embedCalls;
    /// <summary>Số lần EmbedAsync được gọi.</summary>
    public int EmbedCalls => Volatile.Read(ref _embedCalls);
    private readonly object _lock = new();
    private readonly List<GeminiRequest> _requests = [];
    private Exception? _failNext;

    public System.Collections.Concurrent.ConcurrentQueue<IReadOnlyList<GeminiChunk>> Script { get; } = new();
    /// <summary>Bản sao (snapshot) các request đã ghi nhận.</summary>
    public IReadOnlyList<GeminiRequest> Requests { get { lock (_lock) return _requests.ToList(); } }
    public string PdfMarkdown { get; set; } = "";
    public Exception? FailNext { get { lock (_lock) return _failNext; } set { lock (_lock) _failNext = value; } }

    public void Reset()
    {
        Script.Clear();
        lock (_lock) { _requests.Clear(); _failNext = null; }
        EmbedOverride = null;
        Volatile.Write(ref _embedCalls, 0);
        PdfMarkdown = "";
    }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, EmbeddingTask task, CancellationToken ct)
    {
        Interlocked.Increment(ref _embedCalls);
        ThrowIfFailing();
        return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(t => (EmbedOverride ?? HashEmbed)(t)).ToList());
    }

    public async IAsyncEnumerable<GeminiChunk> StreamGenerateAsync(GeminiRequest req, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        ThrowIfFailing();
        lock (_lock) _requests.Add(req);
        if (!Script.TryDequeue(out var turn))
            throw new InvalidOperationException("FakeGeminiClient.Script đã hết lượt soạn sẵn.");
        foreach (var chunk in turn)
        {
            await Task.Yield();
            ct.ThrowIfCancellationRequested();
            if (chunk is ThrowChunk t) throw t.Exception;
            yield return chunk;
        }
    }

    public Task<string> PdfToMarkdownAsync(byte[] pdf, CancellationToken ct)
    {
        ThrowIfFailing();
        return Task.FromResult(PdfMarkdown);
    }

    /// <summary>Băm từng từ (không phân biệt hoa thường) vào 768 chiều rồi chuẩn hóa L2: cùng tập từ thì cosine = 1.</summary>
    public static float[] HashEmbed(string text)
    {
        var v = new float[Dimensions];
        var words = text.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('.', ',', ';', ':', '!', '?', '"', '(', ')')).Where(w => w.Length > 0).Distinct();
        foreach (var w in words)
            v[(int)(BitConverter.ToUInt32(SHA256.HashData(Encoding.UTF8.GetBytes(w)), 0) % Dimensions)] += 1f;
        var norm = MathF.Sqrt(v.Sum(x => x * x));
        if (norm > 0) for (var i = 0; i < v.Length; i++) v[i] /= norm;
        return v;
    }

    private void ThrowIfFailing()
    {
        Exception? ex;
        lock (_lock) { ex = _failNext; _failNext = null; }
        if (ex is not null) throw ex;
    }
}
