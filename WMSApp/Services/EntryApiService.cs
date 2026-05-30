using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 普通入库流程的后端 API 客户端。
    /// </summary>
    public class EntryApiService : IEntryApiService
    {
        /// <summary>
        /// 访问 SmartFactory 后端接口的 HTTP 客户端。
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// 初始化普通入库 API 服务，并绑定后端基础地址。
        /// </summary>
        public EntryApiService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("SmartFactoryApi");
        }

        /// <summary>
        /// 按扫描条码和起始库位请求后端预分配入库库位。
        /// </summary>
        public async Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string binNo)
        {
            var request = new
            {
                barcode,
                binNo
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/entry/allocate", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<IEnumerable<PalletBarRelation>>>();
                return result ?? Result<IEnumerable<PalletBarRelation>>.Fail("分配接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail(ex.Message);
            }
        }

        /// <summary>
        /// 提交已确认的入库库位分配结果，并返回后端事务处理结果。
        /// </summary>
        public async Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation)
        {
            var request = new
            {
                Items = items,
                WarehouseLocation = warehouseLocation
            };

            try
            {
                using var response = await _httpClient.PostAsJsonAsync("api/entry/commit", request);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadFromJsonAsync<Result<IEnumerable<PalletBarRelation>>>();
                return result ?? Result<IEnumerable<PalletBarRelation>>.Fail("入库接口返回为空。");
            }
            catch (Exception ex)
            {
                return Result<IEnumerable<PalletBarRelation>>.Fail(ex.Message);
            }
        }
    }
}
