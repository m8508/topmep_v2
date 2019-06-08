using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Topmep.Models;

namespace Topmep.Service
{

    public class TypeSelectComponet
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        //主系統、次系統、九宮格第一階選單
        public static Dictionary<string, object> getMapItemQueryCriteria(string id)
        {
            Dictionary<string, object> selectComponent = new Dictionary<string, object>();
            List<SelectListItem> selectMain = new List<SelectListItem>();
            PurchaseFormService service = new PurchaseFormService();
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
            selectComponent.Add("SystemMain", selectMain);

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
            //selectSub.Add(empty);
            selectComponent.Add("SystemSub", selectSub);


            TypeManageService typeService = new TypeManageService();
            List<REF_TYPE_MAIN> lstType1 = typeService.getTypeMainL1();

            //取得九宮格
            List<SelectListItem> selectType1 = new List<SelectListItem>();
            for (int idx = 0; idx < lstType1.Count; idx++)
            {
                log.Debug("REF_TYPE_MAIN=" + idx + "," + lstType1[idx].CODE_1_DESC);
                SelectListItem selectI = new SelectListItem();
                selectI.Value = lstType1[idx].TYPE_CODE_1;
                selectI.Text = lstType1[idx].CODE_1_DESC;
                selectType1.Add(selectI);
            }
            selectComponent.Add("TypeCodeL1", selectType1);
            return selectComponent;
        }
        //取得圖算數量
        public static void getMapItem(FormCollection f, out string projectid, out string typeCode1, out string typeCode2, out string systemMain, out string systemSub, out string primeside, out string primesideName, out string secondside, out string secondsideName, out string mapno, out string buildno, out string devicename, out string mapType, out string strart_id, out string end_id)
        {
            projectid = f["projectid"].Trim();
            log.Debug("projectid" + f["projectid"]);

            typeCode1 = null;
            if (null != f["TypeCodeL2"])
            {
                typeCode1 = f["TypeCodeL2"].Trim();
            }

            typeCode2 = null;
            if (null != f["TypeSub"])
            {
                typeCode2 = f["TypeSub"].Trim();
            }
            systemMain = null;
            if (null != f["systemMain"])
            {
                systemMain = f["systemMain"].Trim();
            }
            systemSub = null;
            if (null != f["systemSub"])
            {
                systemSub = f["systemSub"].Trim();
            }

            log.Debug("typeCode1=" + typeCode1 + ",typeCode2=" + typeCode2 + ",systemMain=" + systemMain + "systemSub=" + systemSub);

            primeside = f["primeside"];
            log.Debug("primeside" + f["primeside"]);
            primesideName = f["primesideName"];
            log.Debug("primesideName" + primesideName);

            secondside = f["secondside"];
            log.Debug("secondside" + f["secondside"]);
            secondsideName = f["secondsideName"];
            log.Debug("secondsideName" + secondsideName);


            mapno = f["mapno"];
            log.Debug("mapno" + f["mapno"]);
            buildno = f["buildno"];
            log.Debug("buildno" + f["buildno"]);
            devicename = f["devicename"];
            log.Debug("devicename" + f["devicename"]);

            mapType = f["mapType"];
            log.Debug("mapType" + f["mapType"]);
            strart_id = f["startid"];
            end_id = f["endid"];
        }

    }
}