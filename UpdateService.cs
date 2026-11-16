using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace Tagsor
{
    public class UpdateInfo
    {
        public string VersionTag { get; set; } = "";
        public string ReleaseUrl { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public bool IsNewerAvailable { get; set; }
    }

    public static class UpdateService
    {
        private const string GitHubLatestReleaseApi = "https://api.github.com/repos/zakiuhh/tagsor/releases/latest";

        public static string CurrentVersion
        {
            get
            {
                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                return ver != null ? $"{ver.Major}.{ver.Minor}.{ver.Build}" : "1.4.0";
            }
        }

        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Tagsor-Updater/1.4");
            return client;
        }

        public static async Task<UpdateInfo?> CheckForUpdatesAsync()
        {
            try
            {
                using var response = await _http.GetAsync(GitHubLatestReleaseApi);
                if (!response.IsSuccessStatusCode) return null;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tagName = root.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var urlProp) ? urlProp.GetString() ?? "https://github.com/zakiuhh/tagsor/releases" : "https://github.com/zakiuhh/tagsor/releases";

                string downloadUrl = "";
                if (root.TryGetProperty("assets", out var assetsProp) && assetsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var asset in assetsProp.EnumerateArray())
                    {
                        string name = asset.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadUrl = asset.TryGetProperty("browser_download_url", out var d) ? d.GetString() ?? "" : "";
                            break;
                        }
                    }
                }

                bool isNewer = IsRemoteVersionNewer(tagName, CurrentVersion);

                return new UpdateInfo
                {
                    VersionTag = tagName,
                    ReleaseUrl = htmlUrl,
                    DownloadUrl = downloadUrl,
                    IsNewerAvailable = isNewer
                };
            }
            catch
            {
                return null;
            }
        }

        private static bool IsRemoteVersionNewer(string remoteTag, string localVersionStr)
        {
            string cleanRemote = remoteTag.Trim().TrimStart('v', 'V');
            int dashIdx = cleanRemote.IndexOf('-');
            if (dashIdx > 0) cleanRemote = cleanRemote[..dashIdx];

            if (Version.TryParse(cleanRemote, out var remoteVer) && Version.TryParse(localVersionStr, out var localVer))
            {
                // Normalize Revision (-1 vs 0) so 1.4.0 == 1.4.0.0
                var normRemote = new Version(remoteVer.Major, Math.Max(0, remoteVer.Minor), Math.Max(0, remoteVer.Build));
                var normLocal = new Version(localVer.Major, Math.Max(0, localVer.Minor), Math.Max(0, localVer.Build));
                return normRemote > normLocal;
            }
            return false;
        }

        public static async Task<bool> DownloadAndInstallUpdateAsync(string downloadUrl, IProgress<double>? progress = null)
        {
            try
            {
                string? currentExePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;
                if (string.IsNullOrEmpty(currentExePath) || currentExePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                string tempDir = Path.GetTempPath();
                string stagedNewExe = Path.Combine(tempDir, $"Tagsor_new_{Guid.NewGuid():N}.exe");
                string updaterScript = Path.Combine(tempDir, $"Tagsor_updater_{Guid.NewGuid():N}.ps1");

                using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    long totalBytes = response.Content.Headers.ContentLength ?? -1L;

                    await using var contentStream = await response.Content.ReadAsStreamAsync();
                    await using var fileStream = new FileStream(stagedNewExe, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                    var buffer = new byte[81920];
                    long totalRead = 0;
                    int read;
                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        if (totalBytes > 0)
                        {
                            progress?.Report((double)totalRead / totalBytes * 100.0);
                        }
                    }
                }

                int currentPid = Environment.ProcessId;
                string escapedOldExe = currentExePath.Replace("'", "''");
                string escapedNewExe = stagedNewExe.Replace("'", "''");
                string escapedScript = updaterScript.Replace("'", "''");

                // PowerShell script:
                // 1. Waits until the old Tagsor.exe process completely exits
                // 2. Deletes the old Tagsor.exe (retrying briefly if Windows Defender holds a lock)
                // 3. Moves the newly downloaded exe into the exact original path
                // 4. Launches the new Tagsor.exe and deletes this temporary script
                string psScriptContent = $@"
$ErrorActionPreference = 'SilentlyContinue'
try {{
    $proc = Get-Process -Id {currentPid} -ErrorAction SilentlyContinue
    if ($proc) {{ $proc.WaitForExit(15000) | Out-Null }}
}} catch {{}}

Start-Sleep -Milliseconds 350

for ($i = 0; $i -lt 20; $i++) {{
    if (Test-Path -LiteralPath '{escapedOldExe}') {{
        Remove-Item -LiteralPath '{escapedOldExe}' -Force -ErrorAction SilentlyContinue
    }}
    if (-not (Test-Path -LiteralPath '{escapedOldExe}')) {{
        break
    }}
    Start-Sleep -Milliseconds 250
}}

Move-Item -LiteralPath '{escapedNewExe}' -Destination '{escapedOldExe}' -Force
Start-Process -FilePath '{escapedOldExe}' -ArgumentList '--updated'
Remove-Item -LiteralPath '{escapedScript}' -Force -ErrorAction SilentlyContinue
";

                await File.WriteAllTextAsync(updaterScript, psScriptContent);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{updaterScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void CleanupLeftoverTempFiles()
        {
            try
            {
                string tempDir = Path.GetTempPath();
                foreach (var file in Directory.EnumerateFiles(tempDir, "Tagsor_new_*.exe"))
                {
                    try { File.Delete(file); } catch { }
                }
                foreach (var script in Directory.EnumerateFiles(tempDir, "Tagsor_updater_*.ps1"))
                {
                    try { File.Delete(script); } catch { }
                }
            }
            catch { }
        }
    }
}
