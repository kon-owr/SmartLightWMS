using WMSApp.DTO;
using System.Threading;
using System.Threading.Tasks;

namespace WMSApp.Services
{
    public interface IAppUpdateInstaller
    {
        Task<Result<bool>> DownloadAndInstallAsync(UpdateCheckResponse updateInfo, CancellationToken cancellationToken = default);
    }
}
