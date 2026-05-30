using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using WMSApp.DTO;

namespace WMSApp.Services
{
    /// <summary>
    /// 通过系统默认浏览器打开更新包下载地址的通用安装器。
    /// </summary>
    public class BrowserUpdateInstaller : IAppUpdateInstaller
    {
        /// <summary>
        /// 打开更新下载链接，并把系统启动失败转换为可展示的结果。
        /// </summary>
        public Task<Result<bool>> DownloadAndInstallAsync(UpdateCheckResponse updateInfo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                return Task.FromResult(Result<bool>.Fail("下载地址为空。"));
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = updateInfo.DownloadUrl,
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                return Task.FromResult(Result<bool>.Ok(true, "已打开下载链接。"));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<bool>.Fail($"无法打开下载链接: {ex.Message}"));
            }
        }
    }
}
