using System;
using System.Collections.Generic;

namespace WMSApp.DTO
{
    public class InductionShelfValidateRequest
    {
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
    }

    public class InductionShelfValidation
    {
        public bool IsValid { get; set; }
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseNo { get; set; } = string.Empty;
        public int EmptyLocationCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class InductionDepositRequest
    {
        public string Barcode { get; set; } = string.Empty;
        public string ShelfCode { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
    }

    public class DepositCallbackMessage
    {
        public bool Success { get; set; }
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class DepositedItem
    {
        public string BarNo { get; set; } = string.Empty;
        public string BinNo { get; set; } = string.Empty;
        public decimal BarQty { get; set; }
        public DateTime DepositTime { get; set; }
        public int Status { get; set; }
    }

    public class InductionPickQueryRequest
    {
        public string ItemNo { get; set; } = string.Empty;
        public decimal? RequiredQty { get; set; }
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Color { get; set; } = 6;
    }

    public class InductionPickStartRequest
    {
        public List<string> LabelIds { get; set; } = new();
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Color { get; set; } = 6;
    }

    public class InductionPickSuggestionRequest
    {
        public string Keyword { get; set; } = string.Empty;
        public string WarehouseLocation { get; set; } = string.Empty;
        public int Limit { get; set; } = 20;
    }

    public class PickCallbackMessage
    {
        public bool Success { get; set; }
        public string LabelId { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsIllegal { get; set; }
    }

    public class InductionPickItem
    {
        public string BarNo { get; set; } = string.Empty;
        public string ItemNo { get; set; } = string.Empty;
        public decimal BarQty { get; set; }
        public string BinNo { get; set; } = string.Empty;
        public DateTime InstockDate { get; set; }
        public int Status { get; set; }

        public string StatusText => Status switch
        {
            0 => "待出库",
            1 => "已出库",
            2 => "非法出库",
            _ => "未知"
        };

        public string StatusClass => Status switch
        {
            0 => "Solid Yellow",
            1 => "Solid Green",
            2 => "Solid Red",
            _ => "Solid Grey"
        };

        public bool IsPending => Status == 0;
        public bool IsCompleted => Status == 1;
        public bool IsIllegalStatus => Status == 2;
        public bool IsUnknownStatus => Status != 0 && Status != 1 && Status != 2;
    }

    public class InductionCancelRequest
    {
        public string Barcode { get; set; } = string.Empty;
    }

    public class InductionPickCancelRequest
    {
        public List<string> LabelIds { get; set; } = new();
    }
}
