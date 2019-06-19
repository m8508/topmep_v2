using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Topmep.Models;

namespace Topmep.Service
{
    public class UtilService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public static List<SelectListItem> getMainSystem(string id, InquiryFormService service)
        {
            //取得主系統資料
            List<SelectListItem> selectMain = new List<SelectListItem>();
            foreach (string itm in service.getSystemMain(id))
            {
                log.Debug("Main System=" + itm);
                SelectListItem selectI = new SelectListItem();
                selectI.Value = itm;
                selectI.Text = itm;
                if (null != itm && "" != itm)
                {
                    selectMain.Add(selectI);
                }
            }
            return selectMain;
        }
        public static List<SelectListItem> getSubSystem(string id, InquiryFormService service)
        {
            //取得次系統資料
            List<SelectListItem> selectSub = new List<SelectListItem>();
            foreach (string itm in service.getSystemSub(id))
            {
                log.Debug("Sub System=" + itm);
                SelectListItem selectI = new SelectListItem();
                selectI.Value = itm;
                selectI.Text = itm;
                if (null != itm && "" != itm)
                {
                    selectSub.Add(selectI);
                }
            }

            return selectSub;
        }
        public static string covertToJson(Object obj)
        {
            System.Web.Script.Serialization.JavaScriptSerializer objSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            string itemJson = objSerializer.Serialize(obj);
            log.Debug(itemJson);
            return itemJson;
        }
        public static SYS_USER GetUserInfoFromSession( HttpSessionStateBase Session )
        {
            SYS_USER u = ((UserService)Session["UserService"]).loginUser;
            return u;
        }
    }
}