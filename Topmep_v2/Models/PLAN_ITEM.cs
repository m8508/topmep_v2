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
    
    public partial class PLAN_ITEM
    {
        public string PLAN_ITEM_ID { get; set; }
        public string PROJECT_ID { get; set; }
        public string ITEM_ID { get; set; }
        public string ITEM_DESC { get; set; }
        public string ITEM_UNIT { get; set; }
        public Nullable<decimal> ITEM_QUANTITY { get; set; }
        public Nullable<decimal> ITEM_UNIT_PRICE { get; set; }
        public Nullable<decimal> MAN_PRICE { get; set; }
        public string ITEM_REMARK { get; set; }
        public string TYPE_CODE_1 { get; set; }
        public string TYPE_CODE_2 { get; set; }
        public string SUB_TYPE_CODE { get; set; }
        public string SYSTEM_MAIN { get; set; }
        public string SYSTEM_SUB { get; set; }
        public string MODIFY_USER_ID { get; set; }
        public Nullable<System.DateTime> MODIFY_DATE { get; set; }
        public string CREATE_USER_ID { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
        public Nullable<long> EXCEL_ROW_ID { get; set; }
        public string FORM_NAME { get; set; }
        public string SUPPLIER_ID { get; set; }
        public Nullable<decimal> BUDGET_RATIO { get; set; }
        public Nullable<decimal> ITEM_FORM_QUANTITY { get; set; }
        public Nullable<decimal> ITEM_UNIT_COST { get; set; }
        public Nullable<decimal> TND_RATIO { get; set; }
        public string MAN_FORM_NAME { get; set; }
        public string MAN_SUPPLIER_ID { get; set; }
        public Nullable<decimal> LEAD_TIME { get; set; }
        public string DEL_FLAG { get; set; }
        public string INQUIRY_FORM_ID { get; set; }
        public string MAN_FORM_ID { get; set; }
        public Nullable<decimal> BUDGET_WAGE_RATIO { get; set; }
        public string IN_CONTRACT { get; set; }
    }
}
