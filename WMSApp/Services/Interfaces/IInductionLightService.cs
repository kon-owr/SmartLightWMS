using System.Threading.Tasks;

namespace WMSApp.Services
{
    /// <summary>
    /// 封装感应料架的亮灯与熄灯能力。
    /// </summary>
    public interface IInductionLightService
    {
        /// <summary>
        /// 点亮指定料架的所有空库位。
        /// </summary>
        Task<string> LightOnAllEmptyLocationAsync(string shelfCode, int color);

        /// <summary>
        /// 熄灭指定料架的所有空库位。
        /// </summary>
        Task<string> LightOffAllEmptyLocationAsync(string shelfCode);
    }
}
