using System.Net;
using System.Runtime.InteropServices;
using HorizonRadioOverlay.Services;

namespace HorizonRadioOverlay.Tests;

public sealed class UpdateServiceTests
{
    [Theory]
    [InlineData(Architecture.X86, "x86.exe")]
    [InlineData(Architecture.X64, "x64.zip")]
    [InlineData(Architecture.Arm64, "arm64.exe")]
    [InlineData(Architecture.Arm, "release")]
    public async Task Update_link_matches_os_architecture_or_falls_back_to_release(Architecture architecture, string expected)
    {
        using var http = new HttpClient(new ReleaseHandler("""
            {"tag_name":"v99.0.0","html_url":"https://example.invalid/release","assets":[
              {"name":"app_win-x64.zip","browser_download_url":"https://example.invalid/x64.zip"},
              {"name":"app_win-x86.exe","browser_download_url":"https://example.invalid/x86.exe"},
              {"name":"app_win-arm64.exe","browser_download_url":"https://example.invalid/arm64.exe"}
            ]}
            """));
        using var service = new UpdateService(http, architecture);
        var result = await service.CheckForUpdateAsync();
        Assert.True(result.HasUpdate);
        Assert.Equal("https://example.invalid/" + expected, result.DownloadUrl);
    }

    [Fact]
    public async Task Missing_architecture_does_not_offer_an_incompatible_package_or_checksum()
    {
        using var http = new HttpClient(new ReleaseHandler("""
            {"tag_name":"v99.0.0","html_url":"https://example.invalid/release","assets":[
              {"name":"app_win-x64.exe","browser_download_url":"https://example.invalid/x64.exe"},
              {"name":"app_win-arm64.exe.sha256","browser_download_url":"https://example.invalid/hash"}
            ]}
            """));
        using var service = new UpdateService(http, Architecture.Arm64);
        Assert.Equal("https://example.invalid/release", (await service.CheckForUpdateAsync()).DownloadUrl);
    }

    private sealed class ReleaseHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
