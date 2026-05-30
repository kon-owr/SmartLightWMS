using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 负责维护客户端与感应料架 Hub 的长连接，并把回调事件分发给 ViewModel。
    /// </summary>
    public class InductionHubService : IInductionHubService, IAsyncDisposable
    {
        /// <summary>
        /// 维护与后端感应 Hub 的 SignalR 长连接。
        /// </summary>
        private readonly HubConnection _hubConnection;

        /// <summary>
        /// 标记服务是否已经释放，防止释放后继续启动连接。
        /// </summary>
        private bool _isDisposed;

        public event EventHandler<DepositCallbackMessage>? DepositCallbackReceived;
        public event EventHandler<PickCallbackMessage>? PickCallbackReceived;

        public bool IsConnected => _hubConnection.State == HubConnectionState.Connected;

        /// <summary>
        /// 根据后端地址创建 Hub 连接，注册入库和拣货回调分发逻辑。
        /// </summary>
        public InductionHubService(IConfiguration configuration)
        {
            var apiBase = configuration["Api:SmartFactory:BaseAddress"]
                ?? throw new InvalidOperationException("缺少配置 Api:SmartFactory:BaseAddress");

            var hubUrl = $"{apiBase.TrimEnd('/')}/hubs/induction";
            _hubConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl)
                .WithAutomaticReconnect(new[]
                {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            _hubConnection.On<DepositCallbackMessage>("ReceiveDepositCallback", message =>
            {
                DepositCallbackReceived?.Invoke(this, message);
            });

            _hubConnection.On<PickCallbackMessage>("ReceivePickCallback", message =>
            {
                PickCallbackReceived?.Invoke(this, message);
            });

            _hubConnection.Reconnecting += error =>
            {
                Console.WriteLine($"SignalR reconnecting: {error?.Message}");
                return Task.CompletedTask;
            };

            _hubConnection.Reconnected += connectionId =>
            {
                Console.WriteLine($"SignalR reconnected: {connectionId}");
                return Task.CompletedTask;
            };

            _hubConnection.Closed += error =>
            {
                Console.WriteLine($"SignalR connection closed: {error?.Message}");
                return Task.CompletedTask;
            };
        }

        /// <summary>
        /// 在页面需要接收感应回调时启动 SignalR 连接。
        /// </summary>
        public async Task StartAsync()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(InductionHubService));
            }

            if (_hubConnection.State == HubConnectionState.Disconnected)
            {
                await _hubConnection.StartAsync();
                Console.WriteLine("SignalR connected");
            }
        }

        /// <summary>
        /// 在页面关闭或不再需要回调时停止 SignalR 连接。
        /// </summary>
        public async Task StopAsync()
        {
            if (_hubConnection.State != HubConnectionState.Disconnected)
            {
                await _hubConnection.StopAsync();
                Console.WriteLine("SignalR disconnected");
            }
        }

        /// <summary>
        /// 释放 Hub 连接资源，供应用关闭或容器销毁时调用。
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            await _hubConnection.DisposeAsync();
        }
    }
}
