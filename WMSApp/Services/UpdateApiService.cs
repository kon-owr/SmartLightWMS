using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Threading.Tasks;
using WMSApp.DTO;
using System.Linq;

namespace WMSApp.Services
{
    /// <summary>
    /// 应用更新检查的后端 API 客户端。
    /// </summary>
    public class UpdateApiService : IUpdateApiService
    {
        /// <summary>
        /// 访问 SmartFactory 后端更新接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化更新检查 API 服务，并绑定后端基础地址。
        /// </summary>
        public UpdateApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 按应用、平台、当前版本和发布通道检查是否存在可用更新。
        /// </summary>
        public async Task<Result<UpdateCheckResponse>> CheckAsync(string appId, string platform, int currentVersionCode, string? channel = null)
        {
            try
            {
                var query = $"api/update/check?appId={Uri.EscapeDataString(appId)}&platform={Uri.EscapeDataString(platform)}&currentVersionCode={currentVersionCode}";
                if (!string.IsNullOrWhiteSpace(channel))
                {
                    query += $"&channel={Uri.EscapeDataString(channel)}";
                }

                using var response = await _httpClient.GetAsync(query);

                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    var notFoundResult = await response.Content.ReadFromJsonAsync<Result<UpdateCheckResponse>>();
                    return notFoundResult ?? Result<UpdateCheckResponse>.Fail("No release found for specified app/platform/channel.");
                }

                if (response.StatusCode == HttpStatusCode.BadRequest)
                {
                    var badRequestResult = await response.Content.ReadFromJsonAsync<Result<UpdateCheckResponse>>();
                    if (badRequestResult != null)
                    {
                        return badRequestResult;
                    }

                    return Result<UpdateCheckResponse>.Fail("更新检查参数无效。");
                }

                response.EnsureSuccessStatusCode();
                var result = await response.Content.ReadFromJsonAsync<Result<UpdateCheckResponse>>() ?? Result<UpdateCheckResponse>.Fail("更新检查响应为空。");
                if (result.Success && result.Data != null)
                {
                    result.Data.DownloadUrl = NormalizeDownloadUrl(result.Data.DownloadUrl);
                }

                return result;
            }
            catch (Exception ex)
            {
                return Result<UpdateCheckResponse>.Fail($"检查更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 将后端返回的同主机默认端口下载地址修正为当前 API 端口。
        /// </summary>
        private string NormalizeDownloadUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return rawUrl;
            }

            var apiBase = _httpClient.BaseAddress;
            if (apiBase == null)
            {
                return rawUrl;
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var absoluteUri))
            {
                return new Uri(apiBase, rawUrl).ToString();
            }

            if (!string.Equals(absoluteUri.Host, apiBase.Host, StringComparison.OrdinalIgnoreCase))
            {
                return rawUrl;
            }

            var usingDefaultHttpPort = (absoluteUri.Scheme == Uri.UriSchemeHttp && absoluteUri.Port == 80)
                || (absoluteUri.Scheme == Uri.UriSchemeHttps && absoluteUri.Port == 443);

            if (!usingDefaultHttpPort)
            {
                return rawUrl;
            }

            var builder = new UriBuilder(absoluteUri)
            {
                Port = apiBase.Port
            };

            return builder.Uri.ToString();
        }
    }
}
