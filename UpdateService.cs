using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tagsor
{
    public sealed class UpdateInfo
    {
        public string VersionTag { get; init; } = "";
        public string ReleaseUrl { get; init; } = "https://github.com/zakiuhh/tagsor/releases/latest";
        public string? DownloadUrl { get; init; }
        public bool IsNewerAvailable { get; init; }
    }

    public static class UpdateService
    {
        public const string CurrentVersion = "1.3.0"; // Bump to "1.4.0" when publishing v1.4.0
        private const string LatestReleaseApi = "https://api.github.com/repos/zakiuhh/tagsor/releases/latest";

        private static readonly HttpClient _http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Tagsor-Updater/1.4");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            return client;
        }

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using var response = await _http.GetAsync(LatestReleaseApi);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp)
                    ? (tagProp.GetString() ?? "")
                    : "";

                string htmlUrl = root.TryGetProperty("html_url", out var urlProp)
                    ? (urlProp.GetString() ?? "https://github.com/zakiuhh/tagsor/releases/latest")
                    : "https://github.com/zakiuhh/tagsor/releases/latest";

                string? exeDownloadUrl = null;
                if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assets.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? (n.GetString() ?? "") : "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            exeDownloadUrl = asset.TryGetProperty("browser_download_url", out var d)
                                ? d.GetString()
                                : null;
                            break;
                        }
                    }
                }

                bool isNewer = IsRemoteVersionNewer(tagName, CurrentVersion);
                return new UpdateInfo
                {
                    VersionTag = tagName,
                    ReleaseUrl = htmlUrl,
                    DownloadUrl = exeDownloadUrl,
                    IsNewerAvailable = isNewer
                };
            }
            catch
            {
                return null;
            }
        }

        public static bool IsRemoteVersionNewer(string remoteTag, string localVersion)
        {
            string cleanRemote = remoteTag.Trim().TrimStart('v', 'V').Split('-')[0];
            string cleanLocal = localVersion.Trim().TrimStart('v', 'V').Split('-')[0];

            if (Version.TryParse(cleanRemote, out var vRemote) && Version.TryParse(cleanLocal, out var vLocal))
            {
                return vRemote > vLocal;
            }
            return false;
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            try
            {
                string? currentExe = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExe) || currentExe.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    Process.Start(new ProcessStartInfo { FileName = downloadUrl, UseShellExecute = true });
                    return false;
                }

                string tempDir = Path.Combine(Path.GetTempPath(), "TagsorUpdate");
                Directory.CreateDirectory(tempDir);
                string downloadedExe = Path.Combine(tempDir, "Tagsor_new.exe");

                using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(downloadedExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    byte[] buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                        totalRead += read;
                        if (totalBytes > 0)
                        {
                            progress?.Report((double)totalRead / totalBytes * 100.0);
                        }
                    }
                }

                // Write a clean helper script that waits for Tagsor.exe to exit, swaps the file, and relaunches
                string scriptPath = Path.Combine(tempDir, "apply_update.bat");
                int pid = Environment.ProcessId;
                string script = $"""
                    @echo off
                    :waitloop
                    tasklist /FI "PID eq {pid}" 2>NUL | find /I "{pid}" >NUL
                    if "%ERRORLEVEL%"=="0" (
                        timeout /t 1 /nobreak >NUL
                        goto waitloop
                    )
                    move /Y "{downloadedExe}" "{currentExe}"
                    start "" "{currentExe}"
                    del "%~f0"
                    """;

                await File.WriteAllTextAsync(scriptPath, script);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/C \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
