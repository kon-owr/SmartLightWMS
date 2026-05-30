using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 定义客户端版本检查接口。
    /// </summary>
    public interface IUpdateApiService
    {
        /// <summary>
        /// 按应用标识、平台和当前版本号查询可用更新。
        /// </summary>
        Task<Result<UpdateCheckResponse>> CheckAsync(string appId, string platform, int currentVersionCode, string? channel = null);
    }
}
