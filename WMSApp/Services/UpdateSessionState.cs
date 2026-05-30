using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 保存本次应用会话内的更新检查状态，避免首页重复请求和重复弹窗。
    /// </summary>
    public class UpdateSessionState : IUpdateSessionState
    {
        public bool HasCheckedOnHomeEnter { get; set; }
        public bool IsChecking { get; set; }
        public bool OptionalUpdateDismissed { get; set; }
        public UpdateCheckResponse? LastResult { get; set; }
    }
}
