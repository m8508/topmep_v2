//------------------------------------------------------------------------------
// <auto-generated>
//     這個程式碼是由範本產生。
//
//     對這個檔案進行手動變更可能導致您的應用程式產生未預期的行為。
//     如果重新產生程式碼，將會覆寫對這個檔案的手動變更。
// </auto-generated>
//------------------------------------------------------------------------------

namespace Topmep_v2.Models
{
    using System;
    using System.Collections.Generic;
    
    public partial class FIN_LOAN_TRANACTION
    {
        public long TID { get; set; }
        public Nullable<long> BL_ID { get; set; }
        public Nullable<int> TRANSACTION_TYPE { get; set; }
        public Nullable<int> PERIOD { get; set; }
        public Nullable<decimal> AMOUNT { get; set; }
        public Nullable<System.DateTime> EVENT_DATE { get; set; }
        public Nullable<System.DateTime> PAYBACK_DATE { get; set; }
        public string REMARK { get; set; }
        public string CREATE_ID { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
        public string MODIFY_ID { get; set; }
        public Nullable<System.DateTime> MODIFY_DATE { get; set; }
        public string VA_FORM_ID { get; set; }
    }
}
