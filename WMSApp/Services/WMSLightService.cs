using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using WMSApp.Models;

namespace WMSApp.Services
{
    /// <summary>
    /// 调用普通料架灯控接口，并将库位集合转换为外部服务要求的报文格式。
    /// </summary>
    public class WMSLightService : IWMSLightService
    {
        /// <summary>
        /// 访问普通料架灯控服务的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 灯控请求和响应解析共用的 JSON 序列化配置。
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

        /// <summary>
        /// 初始化普通料架灯控服务，并绑定灯控服务基础地址。
        /// </summary>
        public WMSLightService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("LightServiceApi");
        }

        /// <summary>
        /// 将一组库位转换为灯控指令并调用外部接口切换灯光颜色。
        /// </summary>
        public async Task<string> ChangeBinNoLightStatus(List<string> binNoList, LightColorCode lightColor, CancellationToken cancellationToken = default)
        {
            if (binNoList is null)
            {
                throw new ArgumentNullException(nameof(binNoList));
            }

            var lightCommands = binNoList
                .Where(binNo => !string.IsNullOrWhiteSpace(binNo))
                .Select(binNo => new LightCommand(binNo.Trim(), lightColor))
                .ToArray();

            if (lightCommands.Length == 0)
            {
                throw new ArgumentException("需要至少一个有效的 binNo。", nameof(binNoList));
            }

            var jsonData = JsonSerializer.Serialize(lightCommands, SerializerOptions);
            var requestBody = JsonSerializer.Serialize(new { jsonData }, SerializerOptions);

            using var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient
                    .PostAsync("api/services/app/LightBarOtherRuleService/LightUpSomeLampBeads", content, cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return TryGetResultMsg(responseBody);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return "调用接口超时：联系IT检查服务器是否开启";
            }
            catch (HttpRequestException ex)
            {
                return $"调用亮灯接口失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 提取灯控接口响应中的 <c>result.message</c>，解析失败时回退为原始响应正文。
        /// </summary>
        private static string TryGetResultMsg(string responseBody)
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                if (document.RootElement.TryGetProperty("result", out var resultElement) &&
                    resultElement.ValueKind == JsonValueKind.Object &&
                    resultElement.TryGetProperty("message", out var msgElement))
                {
                    return msgElement.GetString() ?? string.Empty;
                }
            }
            catch (JsonException)
            {
                // ignore and fall back to raw body
            }

            return responseBody;
        }

        /// <summary>
        /// 表示发送给灯控服务的单个库位亮灯指令。
        /// </summary>
        private sealed record LightCommand(
            [property: JsonPropertyName("location")] string Location,
            [property: JsonPropertyName("color")] LightColorCode Color);
    }
}
