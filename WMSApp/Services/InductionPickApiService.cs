using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 感应拣货流程的后端 API 客户端。
    /// </summary>
    public class InductionPickApiService : IInductionPickApiService
    {
        /// <summary>
        /// 访问 SmartFactory 后端感应拣货接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化感应拣货 API 服务，并绑定后端基础地址。
        /// </summary>
        public InductionPickApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 按料号、需求数量和仓库查询感应料架上的可出库条码。
        /// </summary>
        public async Task<Result<List<InductionPickItem>>> QueryByItemNoAsync(string itemNo, decimal? requiredQty, string warehouseLocation, int color)
        {
            var request = new InductionPickQueryRequest
            {
                ItemNo = itemNo,
                RequiredQty = requiredQty,
                WarehouseLocation = warehouseLocation,
                Color = color
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/pick/query", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<List<InductionPickItem>>>();
                return result ?? Result<List<InductionPickItem>>.Fail("查询出库条码接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<List<InductionPickItem>>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 根据料号关键字获取当前仓库的感应拣货候选料号。
        /// </summary>
        public async Task<Result<List<string>>> GetItemSuggestionsAsync(string keyword, string warehouseLocation, int limit = 20)
        {
            var request = new InductionPickSuggestionRequest
            {
                Keyword = keyword,
                WarehouseLocation = warehouseLocation,
                Limit = limit
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/pick/item-suggestions", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<List<string>>>();
                return result ?? Result<List<string>>.Fail("料号建议接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<List<string>>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 启动一批感应拣货标签的出库流程，并等待料架回调。
        /// </summary>
        public async Task<Result<string>> StartPickAsync(List<string> labelIds, string warehouseLocation, int color)
        {
            var request = new InductionPickStartRequest
            {
                LabelIds = labelIds,
                WarehouseLocation = warehouseLocation,
                Color = color
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/pick/start", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result ?? Result<string>.Fail("开始拣货接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 取消一批仍在等待处理的感应拣货标签。
        /// </summary>
        public async Task<Result<string>> CancelPickAsync(List<string> labelIds)
        {
            var request = new InductionPickCancelRequest
            {
                LabelIds = labelIds
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/induction/pick/cancel", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<string>>();
                return result ?? Result<string>.Fail("取消拣货接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<string>.Fail(ex.Message);
            }
        }
    }
}
