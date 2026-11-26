using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Tdmu.Advisor.Api.Infrastructure.Storage;

namespace Tdmu.Advisor.Tests;

public class LocalFileStoreTests
{
    private sealed class Env(string root) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Root_with_trailing_separator_saves_reads_and_deletes()
    {
        var root = Path.Combine(Path.GetTempPath(), "tdmu-store-" + Guid.NewGuid().ToString("N"), "uploads") + Path.DirectorySeparatorChar;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:Root"] = root }).Build();
        var store = new LocalFileStore(config, new Env(Path.GetTempPath()));
        try
        {
            var rel = await store.SaveAsync(Guid.NewGuid(), ".md", new MemoryStream([1, 2, 3]), default);
            Assert.Equal([1, 2, 3], await store.ReadAsync(rel, default));
            await store.DeleteAsync(rel, default);
            Assert.Empty(Directory.GetFiles(root));
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync("../x.md", default));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(root))!, true);
        }
    }
}
