using System.Collections.Generic;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 封装拣货页对后端拣货接口的调用。
    /// </summary>
    public interface IPickingApiService
    {
        /// <summary>
        /// 校验领料单是否存在。
        /// </summary>
        Task<Result<bool>> CheckDocExistsAsync(string docNo);

        /// <summary>
        /// 按领料单查询并锁定当前仓库可用条码。
        /// </summary>
        Task<Result<List<VariableItem>>> ReserveBarsByDocNoAsync(string docNo, string warehouseLocation);

        /// <summary>
        /// 提交拣货完成请求，并同步释放库位占用。
        /// </summary>
        Task<Result<bool>> CompletePickingAsync(string docNo, List<string> binNos, string warehouseLocation);

        /// <summary>
        /// 显式锁定当前查询结果中的条码。
        /// </summary>
        Task<Result<bool>> LockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation);

        /// <summary>
        /// 显式释放当前查询结果中的条码锁定。
        /// </summary>
        Task<Result<bool>> UnLockBarsAsync(List<VariableItem> barNoList, string docNo, string warehouseLocation);
    }
}
