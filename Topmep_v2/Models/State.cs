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
    
    public partial class State
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public string Name { get; set; }
        public string Reason { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public string Data { get; set; }
    
        public virtual Job Job { get; set; }
    }
}
