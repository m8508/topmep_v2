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
    
    public partial class WF_PROCESS
    {
        public long PID { get; set; }
        public string PROCESS_CODE { get; set; }
        public string PROCESS_NAME { get; set; }
        public string MODIFY_USER_ID { get; set; }
        public Nullable<System.DateTime> MODIFY_DATE { get; set; }
        public string CREATE_USER_ID { get; set; }
        public Nullable<System.DateTime> CREATE_DATE { get; set; }
        public string STATUS { get; set; }
        public string FORM_URL { get; set; }
    }
}