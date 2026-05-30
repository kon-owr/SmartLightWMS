using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 感应料架入库流程的 API 客户端。
    /// </summary>
    public class InductionEntryApiService : IInductionEntryApiService
    {
        /// <summary>
        /// 访问 SmartFactory 后端感应入库接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化感应入库 API 服务，并绑定后端基础地址。
        /// </summary>
        public InductionEntryApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 请求后端验证料架是否可用于当前仓库的感应入库。
        /// </summary>
        public async Task<Result<InductionShelfValidation>> ValidateShelfAsync(string shelfCode, string warehouseLocation)
        {
            var request = new InductionShelfValidateRequest
            {
                ShelfCode = shelfCode,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/entry/validate-shelf", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<InductionShelfValidation>>();
                return result ?? Result<InductionShelfValidation>.Fail("验证料架接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<InductionShelfValidation>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 向后端提交条码入库请求，并等待后续料架回调完成库存事务。
        /// </summary>
        public async Task<Result<string>> DepositAsync(string barcode, string shelfCode, string warehouseLocation)
        {
            var request = new InductionDepositRequest
            {
                Barcode = barcode,
                ShelfCode = shelfCode,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/entry/deposit", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result ?? Result<string>.Fail("入库接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 请求后端取消指定条码的感应入库等待状态。
        /// </summary>
        public async Task<Result<string>> CancelDepositAsync(string barcode)
        {
            var request = new InductionCancelRequest
            {
                Barcode = barcode
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/entry/cancel", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result ?? Result<string>.Fail("取消入库接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
