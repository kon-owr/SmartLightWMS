using System.Collections.Generic;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    public interface IInductionPickApiService
    {
        Task<Result<List<InductionPickItem>>> QueryByItemNoAsync(string itemNo, decimal? requiredQty, string warehouseLocation, int color);
        Task<Result<List<string>>> GetItemSuggestionsAsync(string keyword, string warehouseLocation, int limit = 20);
        Task<Result<string>> StartPickAsync(List<string> labelIds, string warehouseLocation, int color);
        Task<Result<string>> CancelPickAsync(List<string> labelIds);
    }
}
