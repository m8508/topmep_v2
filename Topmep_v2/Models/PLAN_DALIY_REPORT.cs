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
    
    public partial class PLAN_DALIY_REPORT
    {
        public string REPORT_ID { get; set; }
        public string PROJECT_ID { get; set; }
        public Nullable<System.DateTime> REPORT_DATE { get; set; }
        public string WEATHER { get; set; }
        public string SUMMARY { get; set; }
        public string SCENE_USER_NAME { get; set; }
        public string SUPERVISION_NAME { get; set; }
        public string OWNER_NAME { get; set; }
        public string MODIFY_USER_ID { get; set; }
        public Nullable<System.DateTime> MODIFY_DATE { get; set; }
        public string CREATE_USER_ID { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
    }
}