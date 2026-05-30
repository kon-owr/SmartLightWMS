using System.Threading.Tasks;

namespace WMSApp.ViewModels
{
    public interface IPageLifecycleAware
    {
        Task OnPageOpenedAsync();
        Task OnPageClosedAsync();
    }
}
