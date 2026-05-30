using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 感应料架灯光控制的 API 客户端。
    /// </summary>
    public class InductionLightService : IInductionLightService
    {
        /// <summary>
        /// 访问后端感应灯光接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化感应灯光服务，并绑定 SmartFactory 后端接口地址。
        /// </summary>
        public InductionLightService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 请求后端点亮指定料架的所有空库位，并将失败信息转换为页面可展示文本。
        /// </summary>
        public async Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color)
        {
            var request = new { shelfCode, color };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/light/empty-locations", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result?.Data ?? result?.Message ?? "亮灯成功";
            }
            catch (Exception ex)
            {
                return $"感应料架亮灯失败：{ex.Message}";
            }
        }

        /// <summary>
        /// 请求后端熄灭指定料架的所有空库位，并兜底返回熄灯结果文本。
        /// </summary>
        public async Task<string> LightOffAllEmptyLocationAsync(string shelfCode)
        {
            var request = new { shelfCode, color = 0 };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/light/off-empty-locations", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result?.Data ?? result?.Message ?? "熄灯成功";
            }
            catch (Exception ex)
            {
                return $"感应料架熄灯失败：{ex.Message}";
            }
        }
    }
}
