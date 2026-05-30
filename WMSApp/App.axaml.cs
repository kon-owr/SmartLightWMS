using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using WMSApp.Services;
using WMSApp.ViewModels;
using WMSApp.Views;

namespace WMSApp;

/// <summary>
/// Avalonia 应用入口，负责加载 XAML、配置依赖注入和创建平台主视图。
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// 为 ViewLocator 等无法构造函数注入的场景提供统一的服务入口。
    /// </summary>
    public static IServiceProvider? ServiceProvider { get; private set; }

    /// <summary>
    /// 加载应用级 XAML 资源。
    /// </summary>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// 框架初始化完成后注册配置、HTTP 客户端、业务服务和根视图。
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        var builder = new ConfigurationBuilder();
        var assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// 将嵌入资源配置复制到内存流，避免原始资源流生命周期影响配置加载。
        /// </summary>
        static MemoryStream? LoadResourceToMemory(Assembly asm, string resourceName)
        {
            var stream = asm.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                return null;
            }

            var ms = new MemoryStream();
            stream.CopyTo(ms);
            ms.Position = 0;
            return ms;
        }

        // 先加载基础配置，再按环境叠加覆盖项，最后允许环境变量继续覆盖。
        var baseConfig = LoadResourceToMemory(assembly, "WMSApp.appsettings.json");
        if (baseConfig != null)
        {
            builder.AddJsonStream(baseConfig);
        }

        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";
        var envConfigName = $"WMSApp.appsettings.{environment}.json";
        var envConfig = LoadResourceToMemory(assembly, envConfigName);
        if (envConfig != null)
        {
            builder.AddJsonStream(envConfig);
        }

        builder.AddEnvironmentVariables();
        var configuration = builder.Build();

        services.AddSingleton<IConfiguration>(configuration);

        services.AddHttpClient("SmartFactoryApi", client =>
        {
            var apiBase = configuration["Api:SmartFactory:BaseAddress"];
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                throw new InvalidOperationException("缺少配置 Api:SmartFactory:BaseAddress。");
            }

            client.BaseAddress = new Uri(apiBase, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddHttpClient("LightServiceApi", client =>
        {
            var apiBase = configuration["Api:LightService:BaseAddress"];
            if (string.IsNullOrWhiteSpace(apiBase))
            {
                throw new InvalidOperationException("缺少配置 Api:LightService:BaseAddress。");
            }

            client.BaseAddress = new Uri(apiBase, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // 共享业务服务和页面级 ViewModel 都在这里统一注册，保持各平台入口一致。
        services.AddTransient<IPickingApiService, PickingApiService>();
        services.AddTransient<IWMSLightService, WMSLightService>();
        services.AddTransient<IEntryApiService, EntryApiService>();
        services.AddTransient<IUpdateApiService, UpdateApiService>();
        services.AddSingleton<IUpdateSessionState, UpdateSessionState>();
        services.AddSingleton<IAppUpdateInstaller>(_ => CreatePlatformInstaller());

        services.AddTransient<IInductionEntryApiService, InductionEntryApiService>();
        services.AddTransient<IInductionPickApiService, InductionPickApiService>();
        services.AddSingleton<IInductionHubService, InductionHubService>();
        services.AddTransient<IInductionLightService, InductionLightService>();

        services.AddSingleton<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<PickingCodeViewModel>();
        services.AddTransient<EntryCodeViewModel>();
        services.AddTransient<InductionEntryViewModel>();
        services.AddTransient<InductionPickViewModel>();

        ServiceProvider = services.BuildServiceProvider();
        var mainVm = ServiceProvider.GetRequiredService<MainViewModel>();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow
            {
                DataContext = mainVm
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
        {
            singleViewPlatform.MainView = new MainView
            {
                DataContext = mainVm
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// 避免 Avalonia 和 CommunityToolkit 同时输出同一套数据校验提示。
    /// </summary>
    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    /// <summary>
    /// 接收主题变化事件的预留入口，当前不需要额外同步逻辑。
    /// </summary>
    private void Application_ActualThemeVariantChanged(object? sender, EventArgs e)
    {
    }

    /// <summary>
    /// Android 平台优先使用本地安装器，其余平台回退到通用安装器。
    /// </summary>
    private static IAppUpdateInstaller CreatePlatformInstaller()
    {
        var installerType = Type.GetType("WMSApp.Android.Services.AndroidAppUpdateInstaller, WMSApp.Android");
        if (installerType != null && Activator.CreateInstance(installerType) is IAppUpdateInstaller installer)
        {
            return installer;
        }

        return new BrowserUpdateInstaller();
    }
}
