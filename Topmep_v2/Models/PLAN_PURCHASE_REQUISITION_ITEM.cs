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
    
    public partial class PLAN_PURCHASE_REQUISITION_ITEM
    {
        public long PR_ITEM_ID { get; set; }
        public string PR_ID { get; set; }
        public string PLAN_ITEM_ID { get; set; }
        public Nullable<decimal> NEED_QTY { get; set; }
        public Nullable<System.DateTime> NEED_DATE { get; set; }
        public string REMARK { get; set; }
        public Nullable<decimal> ORDER_QTY { get; set; }
        public Nullable<decimal> RECEIPT_QTY { get; set; }
        public string ITEM_DESC { get; set; }
        public string ITEM_UNIT { get; set; }
    }
}
