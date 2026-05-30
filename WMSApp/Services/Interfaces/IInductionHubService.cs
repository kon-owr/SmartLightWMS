using System;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 管理客户端与感应料架 SignalR Hub 的连接和回调分发。
    /// </summary>
    public interface IInductionHubService
    {
        /// <summary>
        /// 收到感应入库回调时触发。
        /// </summary>
        event EventHandler<DepositCallbackMessage>? DepositCallbackReceived;

        /// <summary>
        /// 收到感应拣货回调时触发。
        /// </summary>
        event EventHandler<PickCallbackMessage>? PickCallbackReceived;

        /// <summary>
        /// 启动 Hub 连接。
        /// </summary>
        Task StartAsync();

        /// <summary>
        /// 停止 Hub 连接。
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 当前连接是否已经建立。
        /// </summary>
        bool IsConnected { get; }
    }
}
