using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WMSApp.ViewModels;

namespace WMSApp;

/// <summary>
/// 根据 ViewModel 类型解析对应的 View，供 Avalonia 运行时完成视图定位。
/// </summary>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    /// <summary>
    /// 根据传入 ViewModel 的类型名创建对应 View 实例。
    /// </summary>
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    /// <summary>
    /// 仅为 ViewModelBase 派生类型启用该模板匹配。
    /// </summary>
    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
