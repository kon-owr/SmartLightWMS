using System.Collections.Generic;
using System.Threading.Tasks;
using WMSApp.DTO;

namespace WMSApp.Services
{
    public interface IEntryApiService
    {
        Task<Result<IEnumerable<PalletBarRelation>>> AllocateAsync(string barcode, string binNo);
        Task<Result<IEnumerable<PalletBarRelation>>> CommitAsync(IEnumerable<PalletBarRelation> items, string warehouseLocation);
    }
}
