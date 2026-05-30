using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 封装感应料架入库流程的 HTTP 接口调用。
    /// </summary>
    public interface IInductionEntryApiService
    {
        /// <summary>
        /// 验证料架是否为当前仓库可用的感应料架。
        /// </summary>
        Task<Result<InductionShelfValidation>> ValidateShelfAsync(string shelfCode, string warehouseLocation);

        /// <summary>
        /// 向后端发起入库请求，由料架完成后续回调。
        /// </summary>
        Task<Result<string>> DepositAsync(string barcode, string shelfCode, string warehouseLocation);

        /// <summary>
        /// 取消尚未完成的感应入库请求。
        /// </summary>
        Task<Result<string>> CancelDepositAsync(string barcode);
    }
}
