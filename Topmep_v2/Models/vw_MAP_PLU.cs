//------------------------------------------------------------------------------
// <auto-generated>
//     這個程式碼是由範本產生。
//
//     對這個檔案進行手動變更可能導致您的應用程式產生未預期的行為。
//     如果重新產生程式碼，將會覆寫對這個檔案的手動變更。
// </auto-generated>
//------------------------------------------------------------------------------

namespace Topmep.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class vw_MAP_PLU
    {
        public long PLU_ID { get; set; }
        public string PROJECT_ID { get; set; }
        public string PROJECT_ITEM_ID { get; set; }
        public string MAP_NO { get; set; }
        public string BUILDING_NO { get; set; }
        public string PRIMARY_SIDE { get; set; }
        public string PRIMARY_SIDE_NAME { get; set; }
        public string SECONDARY_SIDE { get; set; }
        public string SECONDARY_SIDE_NAME { get; set; }
        public string PIPE_NAME { get; set; }
        public Nullable<decimal> PIPE_COUNT_SET { get; set; }
        public Nullable<decimal> PIPE_SET_QTY { get; set; }
        public Nullable<decimal> PIPE_LENGTH { get; set; }
        public Nullable<decimal> PIPE_TOTAL_LENGTH { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
        public string CREATE_ID { get; set; }
        public string EXCEL_ITEM { get; set; }
        public string ITEM_DESC { get; set; }
    }
}
