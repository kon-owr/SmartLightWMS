using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 拣货流程的 API 客户端。
    /// </summary>
    public class PickingApiService : IPickingApiService
    {
        /// <summary>
        /// 访问 SmartFactory 后端普通拣货接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化普通拣货 API 服务，并绑定后端基础地址。
        /// </summary>
        public PickingApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 检查领料单是否存在，用于查询前的单号有效性判断。
        /// </summary>
        public async Task<Result<bool>> CheckDocExistsAsync(string docNo)
        {
            var request = new { docNo };

            try
            {
                return await PostAndReadAsync<bool>("api/pick/exists", request, "领料单存在性接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 按领料单和仓库请求后端分配并锁定可出库条码。
        /// </summary>
        public async Task<Result<List<VariableItem>>> ReserveBarsByDocNoAsync(string docNo, string warehouseLocation)
        {
            var request = new
            {
                DocNo = docNo,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                return await PostAndReadAsync<List<VariableItem>>("api/pick/reserve", request, "查询并锁定接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<List<VariableItem>>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 显式锁定当前页面条码列表，防止其他终端重复分配。
        /// </summary>
        public async Task<Result<bool>> LockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation)
        {
            var request = new
            {
                BarNolist = barNoList,
                DocNo = docNo,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                return await PostAndReadAsync<bool>("api/pick/lock", request, "锁定接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 释放当前页面条码列表的后端锁定记录。
        /// </summary>
        public async Task<Result<bool>> UnLockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation)
        {
            var request = new
            {
                BarNolist = barNoList,
                DocNo = docNo,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                return await PostAndReadAsync<bool>("api/pick/unlock", request, "解锁接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 提交普通拣货完成结果，并让后端释放库位占用和锁定记录。
        /// </summary>
        public async Task<Result<bool>> CompletePickingAsync(string docNo, List<string> binNos, string warehouseLocation)
        {
            var request = new
            {
                DocNo = docNo,
                BinNos = binNos,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                return await PostAndReadAsync<bool>("api/pick/complete", request, "拣货完成接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 统一处理拣货接口的 JSON 请求和结果反序列化。
        /// </summary>
        private async Task<Result<T>> PostAndReadAsync<T>(string url, object request, string emptyMessage)
        {
            using var response = await _httpClient.PostAsJsonAsync(url, request);

            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var badResult = await response.Content.ReadFromJsonAsync<Result<T>>();
                return badResult ?? Result<T>.Fail("请求参数无效。");
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<Result<T>>();
            return result ?? Result<T>.Fail(emptyMessage);
        }
    }
}
