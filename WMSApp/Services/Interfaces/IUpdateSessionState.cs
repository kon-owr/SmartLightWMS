using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 保存当前会话内的更新检查状态，避免首页重复触发相同提示。
    /// </summary>
    public interface IUpdateSessionState
    {
        /// <summary>
        /// 是否已在本次会话进入首页时执行过检查。
        /// </summary>
        bool HasCheckedOnHomeEnter { get; set; }

        /// <summary>
        /// 当前是否正在检查更新。
        /// </summary>
        bool IsChecking { get; set; }

        /// <summary>
        /// 用户是否已忽略本次可选更新提示。
        /// </summary>
        bool OptionalUpdateDismissed { get; set; }

        /// <summary>
        /// 最近一次更新检查结果。
        /// </summary>
        UpdateCheckResponse? LastResult { get; set; }
    }
}
