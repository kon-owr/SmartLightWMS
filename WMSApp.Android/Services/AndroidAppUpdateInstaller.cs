using Android.App;
using Android.Content;
using AndroidX.Core.Content;
using Java.IO;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WMSApp.DTO;
using WMSApp.Services;

namespace WMSApp.Android.Services
{
    public class AndroidAppUpdateInstaller : IAppUpdateInstaller
    {
        public async Task<Result<bool>> DownloadAndInstallAsync(UpdateCheckResponse updateInfo, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(updateInfo.DownloadUrl))
            {
                return Result<bool>.Fail("下载地址为空。" );
            }

            try
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(updateInfo.DownloadUrl, cancellationToken);

                var cacheDir = Application.Context.CacheDir?.AbsolutePath;
                if (string.IsNullOrWhiteSpace(cacheDir))
                {
                    return Result<bool>.Fail("无法访问缓存目录。" );
                }

                var fileName = $"wmsapp-{updateInfo.LatestVersionName}-{updateInfo.LatestVersionCode}.apk";
                var apkPath = Path.Combine(cacheDir, fileName);
                await System.IO.File.WriteAllBytesAsync(apkPath, bytes, cancellationToken);

                if (!string.IsNullOrWhiteSpace(updateInfo.Sha256))
                {
                    using var sha = System.Security.Cryptography.SHA256.Create();
                    using var fs = System.IO.File.OpenRead(apkPath);
                    var hash = sha.ComputeHash(fs);
                    var actual = Convert.ToHexString(hash);
                    if (!string.Equals(actual, updateInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        return Result<bool>.Fail("安装包校验失败，请重新下载。" );
                    }
                }

                var apkFile = new Java.IO.File(apkPath);
                var authority = $"{Application.Context.PackageName}.fileprovider";
                var uri = FileProvider.GetUriForFile(Application.Context, authority, apkFile);

                var intent = new Intent(Intent.ActionView);
                intent.SetDataAndType(uri, "application/vnd.android.package-archive");
                intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
                Application.Context.StartActivity(intent);

                return Result<bool>.Ok(true, "已启动系统安装。" );
            }
            catch (Exception ex)
            {
                return Result<bool>.Fail($"下载或安装失败: {ex.Message}");
            }
        }
    }
}
