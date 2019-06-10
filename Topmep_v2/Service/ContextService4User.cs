using NLog;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Migrations;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using Topmep.Models;
using EntityState = System.Data.Entity.EntityState;

namespace Topmep.Service
{
    /// <summary>
    /// System User service
    /// </summary>
    #region 使用者管理與權限設定
    public class UserService : ContextService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public SYS_USER loginUser = null;
        public List<SYS_MODULE> userMenu = null;
        public List<SYS_FUNCTION> userPrivilege = null;
        public List<SYS_USER> GetProjectUser(string projectid, string tasktype)
        {
            string sql = @"SELECT u.* from TND_TASKASSIGN t ,SYS_USER u
                        where t.USER_ID=u.USER_NAME
                        and t.PROJECT_ID=@projetId
                        and t.TASK_TYPE=@taskType";
            List<SYS_USER> u = null;
            using (var context = new TopmepEntities())
            {
                try
                {
                    log.Debug("sql=" + sql);
                    u = context.SYS_USER.SqlQuery(sql, new SqlParameter("projetId", projectid), new SqlParameter("taskType", tasktype)).ToList();
                }
                catch (Exception e)
                {
                    log.Error("login fail:" + e.StackTrace);
                }
            }
            return u;
        }

        /// <remarks>
        /// User Login by userid and passeword and get provilege informante
        /// </remarks>
        public SYS_USER Login(String userid, String passwd)
        {
            loginUser = null;
            using (var context = new TopmepEntities())
            {
                try
                {
                    loginUser = context.SYS_USER.SqlQuery("select u.* from SYS_USER u "
                        + "where u.USER_ID = @userid "
                        + "and u.PASSWORD = @passwd "
                       , new SqlParameter("userid", userid), new SqlParameter("passwd", passwd)).First();
                }
                catch (Exception e)
                {
                    log.Error("login fail:" + e.StackTrace);
                }
            }
            log.Info("get user info=" + loginUser);
            if (null != loginUser)
            {
                GetPrivilege(userid, passwd);
            }
            return loginUser;
        }

        public void GetPrivilege(String userid, String passwd)
        {
            List<SYS_FUNCTION> privilege = null;
            using (var context = new TopmepEntities())
            {
                string sql = @"select f.* from SYS_FUNCTION f,SYS_PRIVILEGE p,SYS_ROLE r,SYS_USER u 
                                where u.ROLE_ID = r.ROLE_ID
                                and r.ROLE_ID = p.ROLE_ID
                                and p.FUNCTION_ID = f.FUNCTION_ID
                                and u.USER_ID = @userid
                                and u.PASSWORD = @passwd 
                                Order by MODULE_NAME ,SUB_MODULE,FUNCTION_ID;";
                privilege = context.SYS_FUNCTION.SqlQuery(sql, new SqlParameter("userid", userid), new SqlParameter("passwd", passwd)).ToList();
            }
            if (privilege == null || privilege.Count == 0)
            {
                log.Error(userid + " Not Privilege");
                throw new Exception("無設定權限，請洽系統管理員!");
            }
            userPrivilege = privilege;
            userMenu = new List<SYS_MODULE>();
            foreach (SYS_FUNCTION f in privilege)
            {
                SYS_MODULE m = null;
                try
                {
                    m = (from module in userMenu
                         where module.MODULE_NAME == f.MODULE_NAME
                         select module).First();
                }
                catch (Exception ex)
                {
                    log.Warn(ex.Message + ";" + ex.StackTrace);
                }
                if (m == null)
                {
                    m = new SYS_MODULE();
                    m.MODULE_NAME = f.MODULE_NAME;
                    userMenu.Add(m);
                }
                if (f.ISMENU == "Y")
                {
                    m.AddFunction(f);
                }

            }

            log.Info("get functions count=" + userPrivilege.Count);
        }
        //取得供應商資料
        public SYS_USER GetUserInfo(string userid)
        {
            SYS_USER u = null;
            log.Debug("get user by id=" + userid);
            using (var context = new TopmepEntities())
            {
                u = context.SYS_USER.SqlQuery("select u.* from SYS_USER u "
                    + "where u.USER_ID = @userid "
                   , new SqlParameter("userid", userid)).First();
            }
            return u;
        }
    }
    #endregion
    #region 備標處理區段
    /***
     * 備標階段專案管理
     */
    public class TnderProjectService : ContextService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public TND_PROJECT project = null;
        string sno_key = "PROJ";
        public string strMessage = null;
        public TnderProjectService()
        {
        }
        public int newProject(TND_PROJECT prj)
        {
            //1.建立專案基本資料
            log.Info("create new project " + prj.ToString());
            project = prj;
            int i = 0;
            using (var context = new TopmepEntities())
            {
                //2.取得專案編號
                SerialKeyService snoservice = new SerialKeyService();
                project.PROJECT_ID = snoservice.getSerialKey(sno_key);
                log.Info("new projecgt object=" + project.ToString());
                project.STATUS = "備標";
                context.TND_PROJECT.Add(project);
                //3.建立專案存取路徑
                string projectFolder = ContextService.strUploadPath + "/" + project.PROJECT_ID;
                if (Directory.Exists(projectFolder))
                {
                    //資料夾存在
                    log.Info("Directory Exist:" + projectFolder);
                }
                else
                {
                    //if directory not exist create it
                    Directory.CreateDirectory(projectFolder);
                    //把該建的路徑一併建立
                    Directory.CreateDirectory(projectFolder + "/" + quotesFolder);
                }
                i = context.SaveChanges();
                log.Debug("Add project=" + i);
            }
            return i;
        }
        public int updateProject(TND_PROJECT prj)
        {
            //1.建立專案基本資料
            project = prj;
            log.Info("Update project " + project.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.Entry(project).State = EntityState.Modified;
                i = context.SaveChanges();
                log.Debug("Update project=" + i);
            }
            return i;
        }
        public int closeProject(string projectid, string status)
        {
            log.Debug("Close Project ID=" + projectid + ",status=" + status);
            TND_PROJECT p = getProjectById(projectid);
            p.STATUS = status;
            return updateProject(p);
        }
        #region 得標標單項目處理
        public int delAllItemByPlan()
        {
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all item by proejct id=" + project.PROJECT_ID);
                i = context.Database.ExecuteSqlCommand("DELETE FROM PLAN_ITEM WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", project.PROJECT_ID));
            }
            log.Debug("delete item count=" + i);
            return i;
        }
        public int refreshPlanItem(List<PLAN_ITEM> planItem)
        {
            //1.檢查專案是否存在
            if (null == project) { throw new Exception("Project is not exist !!"); }
            int i = 0;
            log.Info("refreshPlanItem = " + planItem.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (PLAN_ITEM item in planItem)
                {
                    item.PROJECT_ID = project.PROJECT_ID;
                    //string strJson = JsonConvert.SerializeObject(item);
                    //log.Debug(strJson);
                    context.PLAN_ITEM.Add(item);
                }
                try
                {
                    i = context.SaveChanges();
                }
                catch (Exception ex)
                {
                    log.Error(ex.StackTrace);
                    log.Error(ex.InnerException.Message + ":" + ex.InnerException.StackTrace);
                }
            }
            log.Info("add plan item count =" + i);
            return i;
        }
        #endregion
        #region 更新消防電圖算數量  
        public int updateMapFP(TND_MAP_FP mapfp)
        {
            //更新消防電圖算資料
            log.Info("Update fp " + mapfp.FP_ID + "," + mapfp.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_MAP_FP.AddOrUpdate(mapfp);
                i = context.SaveChanges();
                log.Debug("Update mapfp=" + i);
            }
            return i;
        }
        #endregion
        #region 更新消防水圖算數量  
        public int updateMapFW(TND_MAP_FW mapfw)
        {
            //更新消防水圖算資料
            log.Info("Update fw " + mapfw.FW_ID + "," + mapfw.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_MAP_FW.AddOrUpdate(mapfw);
                i = context.SaveChanges();
                log.Debug("Update mapfw=" + i);
            }
            return i;
        }
        #endregion
        #region 更新電氣管線圖算數量  
        public int updateMapPEP(TND_MAP_PEP mappep)
        {
            //更新電氣管線圖算資料
            log.Info("Update pep " + mappep.PEP_ID + "," + mappep.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_MAP_PEP.AddOrUpdate(mappep);
                i = context.SaveChanges();
                log.Debug("Update mappep=" + i);
            }
            return i;
        }
        #endregion
        #region 更新弱電管線圖算數量  
        public int updateMapLCP(TND_MAP_LCP maplcp)
        {
            //更新弱電管線圖算資料
            log.Info("Update lcp " + maplcp.LCP_ID + "," + maplcp.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_MAP_LCP.AddOrUpdate(maplcp);
                i = context.SaveChanges();
                log.Debug("Update maplcp=" + i);
            }
            return i;
        }
        #endregion
        #region 更新給排水管線圖算數量  
        public int updateMapPLU(TND_MAP_PLU mapplu)
        {
            //更新給排水圖算資料
            log.Info("Update plu " + mapplu.PLU_ID + "," + mapplu.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_MAP_PLU.AddOrUpdate(mapplu);
                i = context.SaveChanges();
                log.Debug("Update mapplu=" + i);
            }
            return i;
        }
        #endregion
        #region 標單項目處理
        public int delAllItemByProject()
        {
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all item by proejct id=" + project.PROJECT_ID);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_PROJECT_ITEM WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", project.PROJECT_ID));
            }
            log.Debug("delete item count=" + i);
            return i;
        }
        public int refreshProjectItem(List<TND_PROJECT_ITEM> prjItem)
        {
            //1.檢查專案是否存在
            if (null == project) { throw new Exception("Project is not exist !!"); }
            int i = 0;
            log.Info("refreshProjectItem = " + prjItem.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_PROJECT_ITEM item in prjItem)
                {
                    item.PROJECT_ID = project.PROJECT_ID;
                    context.TND_PROJECT_ITEM.Add(item);
                    string strJson = JsonConvert.SerializeObject(item);
                    log.Debug(strJson);
                }
                try
                {
                    i = context.SaveChanges();
                }
                catch (Exception ex)
                {
                    log.Error(ex.StackTrace);
                    strMessage = "匯入失敗(" + ex.Message + ")";
                }
            }
            log.Info("add project item count =" + i);
            return i;
        }
        #endregion
        public string getTaskAssignById(string prjid)
        {
            string projectid = null;
            using (var context = new TopmepEntities())
            {
                projectid = context.Database.SqlQuery<string>("select DISTINCT PROJECT_ID FROM TND_TASKASSIGN WHERE PROJECT_ID = @pid "
               , new SqlParameter("pid", prjid)).FirstOrDefault();
            }
            return projectid;
        }
        //2.建立任務分配表
        public int refreshTask(List<TND_TASKASSIGN> task)
        {
            //1.新增任務分派資料
            int i = 0;
            log.Info("refreshTask = " + task.Count);
            //2.將任務資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_TASKASSIGN item in task)
                {
                    //if (item.FINISH_DATE.ToString() == "")
                    //{
                    //item.FINISH_DATE = DateTime.Now;
                    //}
                    //item.CREATE_DATE = DateTime.Now;
                    context.TND_TASKASSIGN.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add task count =" + i);
            return i;
        }

        public TND_PROJECT getProjectById(string prjid)
        {
            using (var context = new TopmepEntities())
            {
                project = context.TND_PROJECT.SqlQuery("select p.* from TND_PROJECT p "
                    + "where p.PROJECT_ID = @pid "
                   , new SqlParameter("pid", prjid)).First();
            }
            return project;
        }
        //批次產生空白表單
        public int createEmptyForm(string projectid, SYS_USER loginUser)
        {
            int i = 0;
            int i2 = 0;
            using (var context = new TopmepEntities())
            {
                //0.清除所有空白詢價單樣板
                string sql = "DELETE FROM TND_PROJECT_FORM_ITEM WHERE FORM_ID IN (SELECT FORM_ID FROM TND_PROJECT_FORM WHERE SUPPLIER_ID IS NULL AND PROJECT_ID=@projectid);";
                i2 = context.Database.ExecuteSqlCommand(sql, new SqlParameter("projectid", projectid));
                log.Info("delete template inquiry form item  by porjectid=" + projectid + ",result=" + i2);
                sql = "DELETE FROM TND_PROJECT_FORM WHERE SUPPLIER_ID IS NULL AND PROJECT_ID=@projectid; ";
                i2 = context.Database.ExecuteSqlCommand(sql, new SqlParameter("projectid", projectid));
                log.Info("delete template inquiry form  by porjectid=" + projectid + ",result=" + i2);

                //1.依據專案取得九宮格次九宮格分類.
                sql = "SELECT DISTINCT isnull(TYPE_CODE_1,'未分類') TYPE_CODE_1," +
                   "(SELECT TYPE_DESC FROM REF_TYPE_MAIN m WHERE m.TYPE_CODE_1 + m.TYPE_CODE_2 = p.TYPE_CODE_1) as TYPE_CODE_1_NAME, " +
                   "isnull(TYPE_CODE_2,'未分類') TYPE_CODE_2," +
                   "(SELECT TYPE_DESC FROM REF_TYPE_SUB sub WHERE sub.TYPE_CODE_ID = p.TYPE_CODE_1 AND sub.SUB_TYPE_CODE = p.TYPE_CODE_2) as TYPE_CODE_2_NAME " +
                   "FROM TND_PROJECT_ITEM p WHERE PROJECT_ID = @projectid AND ISNULL(DEL_FLAG,'N')='N'  ORDER BY TYPE_CODE_1 ,Type_CODE_2; ";

                List<TYPE_CODE_INDEX> lstType = context.Database.SqlQuery<TYPE_CODE_INDEX>(sql, new SqlParameter("projectid", projectid)).ToList();
                log.Debug("get type index count=" + lstType.Count);
                foreach (TYPE_CODE_INDEX idx in lstType)
                {
                    var parameters = new List<SqlParameter>();
                    parameters.Add(new SqlParameter("projectid", projectid));
                    sql = "SELECT * FROM TND_PROJECT_ITEM WHERE ISNULL(DEL_FLAG,'N')='N' AND PROJECT_ID = @projectid  ";
                    if (idx.TYPE_CODE_1 == "未分類")
                    {
                        sql = sql + "AND TYPE_CODE_1 is null ";
                    }
                    else
                    {
                        sql = sql + "AND TYPE_CODE_1=@typecode1 ";
                        parameters.Add(new SqlParameter("typecode1", idx.TYPE_CODE_1));
                    }

                    if (idx.TYPE_CODE_2 == "未分類")
                    {
                        sql = sql + "AND TYPE_CODE_2 is null ";
                    }
                    else
                    {
                        sql = sql + "AND TYPE_CODE_2=@typecode2 ";
                        parameters.Add(new SqlParameter("typecode2", idx.TYPE_CODE_2));
                    }
                    sql = sql + " ORDER BY EXCEL_ROW_ID";
                    //2.依據分類取得詢價單項次
                    List<TND_PROJECT_ITEM> lstProjectItem = context.TND_PROJECT_ITEM.SqlQuery(sql, parameters.ToArray()).ToList();
                    log.Debug("get project item count=" + lstProjectItem.Count + ", by typecode1=" + idx.TYPE_CODE_1 + ",typeCode2=" + idx.TYPE_CODE_2);
                    string[] itemId = new string[lstProjectItem.Count];
                    int j = 0;
                    foreach (TND_PROJECT_ITEM item in lstProjectItem)
                    {
                        itemId[j] = item.PROJECT_ITEM_ID;
                        j++;
                    }
                    //3.建立詢價單基本資料
                    TND_PROJECT_FORM f = new TND_PROJECT_FORM();
                    if (idx.TYPE_CODE_1 == "未分類")
                    {
                        f.FORM_NAME = "未分類";
                    }
                    else
                    {
                        f.FORM_NAME = idx.TYPE_CODE_1_NAME;
                    }

                    if (idx.TYPE_CODE_2 != "未分類")
                    {
                        f.FORM_NAME = f.FORM_NAME + "-" + idx.TYPE_CODE_2_NAME;
                    }
                    f.FORM_NAME = "(" + idx.TYPE_CODE_1 + "," + idx.TYPE_CODE_2 + ")" + f.FORM_NAME;
                    f.PROJECT_ID = projectid;
                    f.CREATE_ID = loginUser.USER_ID;
                    f.CREATE_DATE = DateTime.Now;
                    f.OWNER_NAME = loginUser.USER_NAME;
                    f.OWNER_EMAIL = loginUser.EMAIL;
                    f.OWNER_TEL = loginUser.TEL + "-" + loginUser.TEL_EXT;
                    f.OWNER_FAX = loginUser.FAX;
                    //4.建立表單
                    string fid = newForm(f, itemId);
                    log.Info("create template form:" + fid);
                    i++;
                }
            }
            log.Info("create form count" + i);
            return i;
        }

        TND_PROJECT_FORM form = null;
        public string newForm(TND_PROJECT_FORM form, string[] lstItemId)
        {
            //1.建立詢價單價單樣本
            log.Info("create new project form ");
            string sno_key = "PO";
            SerialKeyService snoservice = new SerialKeyService();
            form.FORM_ID = snoservice.getSerialKey(sno_key);
            log.Info("new projecgt form =" + form.ToString());
            using (var context = new TopmepEntities())
            {
                context.TND_PROJECT_FORM.Add(form);
                int i = context.SaveChanges();
                log.Debug("Add form=" + i);
                log.Info("project form id = " + form.FORM_ID);
                //if (i > 0) { status = true; };
                List<Topmep.Models.TND_PROJECT_FORM_ITEM> lstItem = new List<TND_PROJECT_FORM_ITEM>();
                string ItemId = "";
                for (i = 0; i < lstItemId.Count(); i++)
                {
                    if (i < lstItemId.Count() - 1)
                    {
                        ItemId = ItemId + "'" + lstItemId[i] + "'" + ",";
                    }
                    else
                    {
                        ItemId = ItemId + "'" + lstItemId[i] + "'";
                    }
                }

                string sql = "INSERT INTO TND_PROJECT_FORM_ITEM (FORM_ID, ITEM_ID ,PROJECT_ITEM_ID, TYPE_CODE, "
                    + "SUB_TYPE_CODE, ITEM_DESC, ITEM_UNIT, ITEM_QTY, ITEM_UNIT_PRICE, ITEM_REMARK) "
                    + "SELECT '" + form.FORM_ID + "' as FORM_ID,ITEM_ID,PROJECT_ITEM_ID, TYPE_CODE_1 AS TYPE_CODE, "
                    + "TYPE_CODE_2 AS SUB_TYPE_CODE, ITEM_DESC, ITEM_UNIT, ITEM_QUANTITY, ITEM_UNIT_PRICE, ITEM_REMARK "
                    + "FROM TND_PROJECT_ITEM where PROJECT_ITEM_ID IN (" + ItemId + ") AND ISNULL(DEL_FLAG,'N')='N'";
                log.Info("sql =" + sql);
                var parameters = new List<SqlParameter>();
                i = context.Database.ExecuteSqlCommand(sql);
                return form.FORM_ID;
            }
        }
        public TND_PROJECT_FORM getProjectFormById(string prjid)
        {
            using (var context = new TopmepEntities())
            {
                form = context.TND_PROJECT_FORM.SqlQuery("select pf.* from TND_PROJECT_FORM pf "
                    + "where pf.PROJECT_ID = @pid"
                   , new SqlParameter("pid", prjid)).First();
            }
            return form;
        }

        public List<TND_TASKASSIGN> getTaskByPrjId(string projectid, string targetRole)
        {
            List<TND_TASKASSIGN> lstTask = new List<TND_TASKASSIGN>();
            using (var context = new TopmepEntities())
            {
                StringBuilder sql = new StringBuilder("select t.* from TND_TASKASSIGN t where t.PROJECT_ID = @projectid");
                var parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("projectid", projectid));
                if (null != targetRole && targetRole != "")
                {
                    sql.Append(" AND TASK_TYPE=@tasktype");
                    parameters.Add(new SqlParameter("tasktype", targetRole));
                }
                sql.Append(" ORDER BY TASK_TYPE");
                log.Debug(sql.ToString());
                lstTask = context.TND_TASKASSIGN.SqlQuery(sql.ToString(), parameters.ToArray()).ToList();
            }
            return lstTask;
        }
        public TaskAssign getTaskById(string taskid)
        {
            TaskAssign lst = new TaskAssign();
            using (var context = new TopmepEntities())
            {
                lst = context.Database.SqlQuery<TaskAssign>("select t.*, convert(varchar, t.FINISH_DATE, 111) as finishDate, "
                    + "convert(varchar, t.CREATE_DATE, 120) as createDate from TND_TASKASSIGN t where t.TASK_ID = @taskid "
                   , new SqlParameter("taskid", taskid)).First();
            }
            return lst;
        }
        #region 更新任務分派資料 
        public int updateTask(TND_TASKASSIGN task)
        {
            //更新任務分派資料
            log.Info("Update task id " + task.TASK_ID);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    if (task.TASK_ID == 0)
                    {
                        context.TND_TASKASSIGN.Add(task);
                    }
                    else
                    {
                        context.TND_TASKASSIGN.AddOrUpdate(task);
                    }
                    i = context.SaveChanges();
                }
                catch (Exception e)
                {
                    log.Debug("Update task=" + i);
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return i;
        }
        public int delTask(SYS_USER user, TND_TASKASSIGN task)
        {
            //刪除任務分派資料
            int i = 0;

            using (var context = new TopmepEntities())
            {
                try
                {
                    TND_TASKASSIGN t = context.TND_TASKASSIGN.Find(task.TASK_ID);
                    context.TND_TASKASSIGN.Remove(t);
                    log.Info(user.USER_NAME + "remove task:" + UtilService.covertToJson(t));
                    i = context.SaveChanges();
                    message = "組織人員已經移除!!";
                }
                catch (Exception e)
                {
                    log.Debug("Remove task=" + i);
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return i;
        }
        #endregion
        public List<TND_PROJECT_FORM_ITEM> getFormItemById(string[] lstItemId)
        {
            List<TND_PROJECT_FORM_ITEM> lstFormItem = new List<TND_PROJECT_FORM_ITEM>();
            int i = 0;
            string ItemId = "";
            for (i = 0; i < lstItemId.Count(); i++)
            {
                if (i < lstItemId.Count() - 1)
                {
                    ItemId = ItemId + "'" + lstItemId[i] + "'" + ",";
                }
                else
                {
                    ItemId = ItemId + "'" + lstItemId[i] + "'";
                }
            }

            using (var context = new TopmepEntities())
            {
                lstFormItem = context.TND_PROJECT_FORM_ITEM.SqlQuery("select f.* from  TND_PROJECT_FORM_ITEM f "
                    + "where f.PROJECT_ITEM_ID in (" + ItemId + ");"
                   , new SqlParameter("ItemId", ItemId)).ToList();
            }

            return lstFormItem;
        }

        //取得標單品項資料
        public List<TND_PROJECT_ITEM> getProjectItem(string checkEx, string projectid, string typeCode1, string typeCode2, string systemMain, string systemSub, string delFlg)
        {
            log.Info("search projectitem by 九宮格 =" + typeCode1 + "search projectitem by 次九宮格 =" + typeCode2 + "search projectitem by 主系統 =" + systemMain + "search projectitem by 次系統 =" + systemSub);
            List<Topmep.Models.TND_PROJECT_ITEM> lstItem = new List<TND_PROJECT_ITEM>();
            //處理SQL 預先填入專案代號,設定集合處理參數
            string sql = "SELECT * FROM TND_PROJECT_ITEM p WHERE p.PROJECT_ID =@projectid ";
            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("projectid", projectid));
            //顯示未分類資料
            if (null != checkEx && checkEx != "")
            {
                sql = sql + "AND p.TYPE_CODE_1 is null or p.TYPE_CODE_1='' ";
            }
            else
            {
                //九宮格
                if (null != typeCode1 && typeCode1 != "")
                {
                    sql = sql + "AND p.TYPE_CODE_1 = @typeCode1 ";
                    parameters.Add(new SqlParameter("typeCode1", typeCode1));
                }
                //次九宮格
                if (null != typeCode2 && typeCode2 != "")
                {
                    sql = sql + "AND p.TYPE_CODE_2 = @typeCode2 ";
                    parameters.Add(new SqlParameter("typeCode2", typeCode2));
                }
                //主系統
                if (null != systemMain && systemMain != "")
                {
                    sql = sql + "AND p.SYSTEM_MAIN LIKE @systemMain ";
                    parameters.Add(new SqlParameter("systemMain", "%" + systemMain + "%"));
                }
                //次系統
                if (null != systemSub && systemSub != "")
                {
                    sql = sql + "AND p.SYSTEM_SUB LIKE @systemSub ";
                    parameters.Add(new SqlParameter("systemSub", "%" + systemSub + "%"));
                }
                //刪除註記
                if ("*" != delFlg)
                {
                    sql = sql + "AND ISNULL(p.DEL_FLAG,'N')=@delFlg ";
                    parameters.Add(new SqlParameter("delFlg", delFlg));
                }
            }
            sql = sql + "  ORDER BY EXCEL_ROW_ID;";
            using (var context = new TopmepEntities())
            {
                log.Debug("get project item sql=" + sql);
                lstItem = context.TND_PROJECT_ITEM.SqlQuery(sql, parameters.ToArray()).ToList();
            }
            log.Info("get projectitem count=" + lstItem.Count);
            return lstItem;
        }
        public string message = "";
        //新增供應商詢價單
        public string addNewSupplierForm(TND_PROJECT_FORM sf, string[] lstItemId)
        {
            string message = "";
            string sno_key = "PO";
            SerialKeyService snoservice = new SerialKeyService();
            sf.FORM_ID = snoservice.getSerialKey(sno_key);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.TND_PROJECT_FORM.AddOrUpdate(sf);
                    i = context.SaveChanges();
                    List<Topmep.Models.TND_PROJECT_FORM_ITEM> lstItem = new List<TND_PROJECT_FORM_ITEM>();
                    string ItemId = "";
                    for (i = 0; i < lstItemId.Count(); i++)
                    {
                        if (i < lstItemId.Count() - 1)
                        {
                            ItemId = ItemId + "'" + lstItemId[i] + "'" + ",";
                        }
                        else
                        {
                            ItemId = ItemId + "'" + lstItemId[i] + "'";
                        }
                    }

                    string sql = "INSERT INTO TND_PROJECT_FORM_ITEM (FORM_ID, PROJECT_ITEM_ID,"
                        + "TYPE_CODE, SUB_TYPE_CODE,ITEM_ID,ITEM_DESC,ITEM_UNIT, ITEM_QTY,"
                        + "ITEM_UNIT_PRICE, ITEM_REMARK) "
                        + "SELECT '" + sf.FORM_ID + "' as FORM_ID, PROJECT_ITEM_ID, TYPE_CODE,"
                        + "SUB_TYPE_CODE, ITEM_ID,ITEM_DESC, ITEM_UNIT,ITEM_QTY, ITEM_UNIT_PRICE,"
                        + "ITEM_REMARK "
                        + "FROM TND_PROJECT_FORM_ITEM where FORM_ITEM_ID IN (" + ItemId + ")";

                    log.Info("sql =" + sql);
                    var parameters = new List<SqlParameter>();
                    i = context.Database.ExecuteSqlCommand(sql);

                }
                catch (Exception e)
                {
                    log.Error("add new supplier form id fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return sf.FORM_ID;
        }

        //更新廠商詢價單單價
        public int refreshSupplierFormItem(string formid, List<TND_PROJECT_FORM_ITEM> lstItem)
        {
            log.Info("Update project supplier inquiry form id =" + formid);
            int j = 0;
            using (var context = new TopmepEntities())
            {
                //將item單價寫入 
                foreach (TND_PROJECT_FORM_ITEM item in lstItem)
                {
                    TND_PROJECT_FORM_ITEM existItem = null;
                    var parameters = new List<SqlParameter>();
                    parameters.Add(new SqlParameter("formid", formid));
                    parameters.Add(new SqlParameter("itemid", item.PROJECT_ITEM_ID));
                    string sql = "SELECT * FROM TND_PROJECT_FORM_ITEM WHERE FORM_ID=@formid AND PROJECT_ITEM_ID=@itemid";
                    log.Info(sql + " ;" + formid + ",project_item_id=" + item.PROJECT_ITEM_ID);
                    TND_PROJECT_FORM_ITEM excelItem = context.TND_PROJECT_FORM_ITEM.SqlQuery(sql, parameters.ToArray()).First();
                    existItem = context.TND_PROJECT_FORM_ITEM.Find(excelItem.FORM_ITEM_ID);
                    log.Debug("find exist item=" + existItem.ITEM_DESC);
                    existItem.ITEM_UNIT_PRICE = item.ITEM_UNIT_PRICE;
                    existItem.ITEM_REMARK = item.ITEM_REMARK;
                    context.TND_PROJECT_FORM_ITEM.AddOrUpdate(existItem);
                }
                j = context.SaveChanges();
                log.Debug("Update project supplier inquiry form item =" + j);
            }
            return j;
        }

        //更新廠商詢價單資料
        public int refreshSupplierForm(string formid, TND_PROJECT_FORM sf, List<TND_PROJECT_FORM_ITEM> lstItem)
        {
            log.Info("Update supplier inquiry form id =" + formid);
            form = sf;
            int i = 0;
            int j = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.Entry(form).State = EntityState.Modified;
                    i = context.SaveChanges();
                    log.Debug("Update supplier inquiry form =" + i);
                    log.Info("supplier inquiry form item = " + lstItem.Count);
                    //將item資料寫入 
                    foreach (TND_PROJECT_FORM_ITEM item in lstItem)
                    {
                        TND_PROJECT_FORM_ITEM existItem = null;
                        log.Debug("form item id=" + item.FORM_ITEM_ID);
                        if (item.FORM_ITEM_ID != 0)
                        {
                            existItem = context.TND_PROJECT_FORM_ITEM.Find(item.FORM_ITEM_ID);
                        }
                        else
                        {
                            var parameters = new List<SqlParameter>();
                            parameters.Add(new SqlParameter("formid", formid));
                            parameters.Add(new SqlParameter("itemid", item.PROJECT_ITEM_ID));
                            string sql = "SELECT * FROM TND_PROJECT_FORM_ITEM WHERE FORM_ID=@formid AND PROJECT_ITEM_ID=@itemid";
                            log.Info(sql + " ;" + formid + ",project_item_id=" + item.PROJECT_ITEM_ID);
                            TND_PROJECT_FORM_ITEM excelItem = context.TND_PROJECT_FORM_ITEM.SqlQuery(sql, parameters.ToArray()).First();
                            existItem = context.TND_PROJECT_FORM_ITEM.Find(excelItem.FORM_ITEM_ID);

                        }
                        log.Debug("find exist item=" + existItem.ITEM_DESC);
                        existItem.ITEM_UNIT_PRICE = item.ITEM_UNIT_PRICE;
                        existItem.ITEM_REMARK = item.ITEM_REMARK;
                        context.TND_PROJECT_FORM_ITEM.AddOrUpdate(existItem);
                    }
                    j = context.SaveChanges();
                    log.Debug("Update supplier inquiry form item =" + j);
                    return 1;
                }
                catch (Exception e)
                {
                    log.Error("update new supplier form id fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return i;
        }

        TND_SUPPLIER supplier = null;
        //取得供應商資料
        public TND_SUPPLIER getSupplierInfo(string supplierName)
        {
            log.Debug("get supplier by id=" + supplierName);
            using (var context = new TopmepEntities())
            {
                try
                {
                    supplier = context.TND_SUPPLIER.SqlQuery("select s.* from TND_SUPPLIER s "
                        + "where s.COMPANY_NAME = @supplierName OR s.SUPPLIER_ID= @supplierName "
                       , new SqlParameter("supplierName", supplierName)).First();
                }
                catch (Exception ex)
                {
                    //針對查不到的廠商資料，忽略相關錯誤
                    log.Error(ex.Message + "," + ex.StackTrace);
                    supplier = new TND_SUPPLIER();
                    supplier.COMPANY_NAME = supplierName;
                }
            }
            return supplier;
        }

        TND_PROJECT_FORM supplierform = null;
        public int newSupplierForm(TND_PROJECT_FORM sf)
        {
            //1.建立廠商詢價單
            log.Info("create new supplier form ");
            string sno_key = "PO";
            supplierform = sf;
            int i = 0;
            using (var context = new TopmepEntities())
            {
                //2.取得廠商詢價單編號
                SerialKeyService snoservice = new SerialKeyService();
                supplierform.FORM_ID = snoservice.getSerialKey(sno_key);
                log.Info("new supplier form object=" + supplierform.ToString());
                context.TND_PROJECT_FORM.Add(supplierform);
                i = context.SaveChanges();
                log.Debug("Add supplier form =" + i);
            }
            return i;
        }

        #region 設備清單圖算數量  
        //設備清單圖算數量  
        public int refreshMapDEVICE(List<TND_MAP_DEVICE> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); } 先註解掉,因為讀取不到project,會造成null == project is true,
            //而導致錯誤, 因為已設定是直接由專案頁面導入上傳圖算畫面，故不會有專案不存在的bug
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_MAP_DEVICE item in items)
                {
                    //item.PROJECT_ID = project.PROJECT_ID;先註解掉,因為專案編號一開始已經設定了，會直接代入
                    context.TND_MAP_DEVICE.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add TND_MAP_DEVICE count =" + i);
            return i;
        }
        public int delMapDEVICEByProject(string projectid)
        {
            log.Info("remove all DEVICE by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_DEVICE by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_MAP_DEVICE WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_DEVICE count=" + i);
            return i;
        }
        #endregion
        #region 消防水圖算數量  
        //增加消防水圖算數量
        public int refreshMapFW(List<TND_MAP_FW> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!" + project); }
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_MAP_FW item in items)
                {
                    context.TND_MAP_FW.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add TND_MAP_FW count =" + i);
            return i;
        }
        public int delMapFWByProject(string projectid)
        {
            log.Info("remove all FW by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_FW by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_MAP_FW WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_FW count=" + i);
            return i;
        }
        #endregion
        #region 給排水圖算數量  
        //增加給排水圖算數量
        public int refreshMapPLU(List<TND_MAP_PLU> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); }
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_MAP_PLU item in items)
                {
                    //item.PROJECT_ID = project.PROJECT_ID;先註解掉,因為專案編號一開始已經設定了，會直接代入
                    log.Info("Item = " + item.PLU_ID + "," + item.PRIMARY_SIDE_NAME);
                    context.TND_MAP_PLU.AddOrUpdate(item);
                }
                try
                {
                    i = context.SaveChanges();
                }
                catch (Exception ex)
                {
                    log.Error(ex.StackTrace);
                    throw ex;
                }
            }
            log.Info("add TND_MAP_PLU count =" + i);
            return i;
        }
        public int delMapPLUByProject(string projectid)
        {
            log.Info("remove all PLU by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_PLU by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_MAP_PLU WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_PLU count=" + i);
            return i;
        }
        #endregion
        #region 弱電管線圖算數量  
        //增加弱電管線圖算數量
        public int refreshMapLCP(List<TND_MAP_LCP> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); }
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_MAP_LCP item in items)
                {
                    //item.PROJECT_ID = project.PROJECT_ID;先註解掉,因為專案編號一開始已經設定了，會直接代入
                    log.Info("Item = " + item.LCP_ID + "," + item.PRIMARY_SIDE_NAME);
                    context.TND_MAP_LCP.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add TND_MAP_LCP count =" + i);
            return i;
        }
        public int delMapLCPByProject(string projectid)
        {
            log.Info("remove all LCP by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_LCP by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_MAP_LCP WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_LCP count=" + i);
            return i;
        }
        #endregion
        #region 電氣管線圖算數量  
        //增加電氣管線圖算數量
        public int refreshMapPEP(List<TND_MAP_PEP> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); }
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            try
            {
                using (var context = new TopmepEntities())
                {
                    foreach (TND_MAP_PEP item in items)
                    {
                        //item.PROJECT_ID = project.PROJECT_ID;
                        context.TND_MAP_PEP.Add(item);
                    }
                    i = context.SaveChanges();
                }
                log.Info("add TND_MAP_PEP count =" + i);
            }
            catch (Exception ex)
            {
                log.Error(ex.StackTrace);
            }
            return i;
        }
        public int delMapPEPByProject(string projectid)
        {
            log.Info("remove all PEP by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_PEP by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_MAP_PEP WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_PEP count=" + i);
            return i;
        }
        #endregion
        #region 消防電圖算數量  
        //消防電圖算數量  
        public int refreshMapFP(List<TND_MAP_FP> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); } 先註解掉,因為讀取不到project,會造成null == project is true,
            //而導致錯誤, 因為已設定是直接由專案頁面導入上傳圖算畫面，故不會有專案不存在的bug
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_MAP_FP item in items)
                {
                    //item.PROJECT_ID = project.PROJECT_ID;先註解掉,因為專案編號一開始已經設定了，會直接代入
                    context.TND_MAP_FP.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add TND_MAP_FP count =" + i);
            return i;
        }
        public int delMapFPByProject(string projectid)
        {
            log.Info("remove all FP by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_MAP_FP by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM  TND_MAP_FP WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_MAP_FP count=" + i);
            return i;
        }
        #endregion
        //取得消防電圖算資料
        public List<MAP_FP_VIEW> getMapFPById(string projectid)
        {
            log.Info("get map FP info by projectid=" + projectid);
            List<MAP_FP_VIEW> lstFP = new List<MAP_FP_VIEW>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstFP = context.Database.SqlQuery<MAP_FP_VIEW>("SELECT FP_ID, EXCEL_ITEM, MAP_NO, BUILDING_NO, PRIMARY_SIDE, PRIMARY_SIDE_NAME, SECONDARY_SIDE, " +
                    "SECONDARY_SIDE_NAME, WIRE_QTY_SET, WIRE_SET_CNT, WIRE_LENGTH, WIRE_TOTAL_LENGTH, PIPE_LENGTH, PIPE_SET, PIPE_LENGTH, " +
                    "PIPE_TOTAL_LENGTH, CREATE_DATE, CREATE_ID, (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID=fp.PIPE_NAME) AS project_item_id, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = fp.WIRE_NAME) AS WIRE_DESC, FP.PROJECT_ITEM_ID A " +
                    "FROM TND_MAP_FP fp WHERE PROJECT_ID = @projectid",
                new SqlParameter("projectid", projectid)).ToList();
            }
            return lstFP;
        }
        //取得消防水圖算資料
        public List<TND_MAP_FW> getMapFWById(string projectid)
        {
            log.Info("get map FW info by projectid=" + projectid);
            List<TND_MAP_FW> lstFW = new List<TND_MAP_FW>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstFW = context.TND_MAP_FW.SqlQuery("SELECT FW_ID, EXCEL_ITEM, MAP_NO, BUILDING_NO, PRIMARY_SIDE, PRIMARY_SIDE_NAME, SECONDARY_SIDE, " +
                    "SECONDARY_SIDE_NAME, PIPE_CNT, PIPE_SET, PIPE_LENGTH, PIPE_TOTAL_LENGTH, PROJECT_ID, PIPE_NAME, CREATE_DATE, CREATE_ID, (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi " +
                    "WHERE tpi.PROJECT_ITEM_ID=fw.PIPE_NAME) AS project_item_id, FW.PROJECT_ITEM_ID A FROM TND_MAP_FW fw WHERE PROJECT_ID = @projectid",
                new SqlParameter("projectid", projectid)).ToList();
            }
            return lstFW;
        }
        //取得給排水圖算資料
        public List<TND_MAP_PLU> getMapPLUById(string projectid)
        {
            log.Info("get map PLU info by projectid=" + projectid);
            List<TND_MAP_PLU> lstPLU = new List<TND_MAP_PLU>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstPLU = context.TND_MAP_PLU.SqlQuery("SELECT PLU_ID, EXCEL_ITEM, MAP_NO, BUILDING_NO, PRIMARY_SIDE, PRIMARY_SIDE_NAME, SECONDARY_SIDE, " +
                    "SECONDARY_SIDE_NAME, PIPE_COUNT_SET, PIPE_SET_QTY, PIPE_LENGTH, PIPE_TOTAL_LENGTH, PROJECT_ID, PIPE_NAME, CREATE_DATE, CREATE_ID, (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi " +
                    "WHERE tpi.PROJECT_ITEM_ID=plu.PIPE_NAME) AS project_item_id, PLU.PROJECT_ITEM_ID A FROM TND_MAP_PLU plu WHERE PROJECT_ID=@projectid",
                new SqlParameter("projectid", projectid)).ToList();
            }
            return lstPLU;
        }
        //取得電氣管線圖算資料
        public List<MAP_PEP_VIEW> getMapPEPById(string projectid)
        {
            log.Info("get map PEP info by projectid=" + projectid);
            List<MAP_PEP_VIEW> lstPEP = new List<MAP_PEP_VIEW>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstPEP = context.Database.SqlQuery<MAP_PEP_VIEW>("SELECT PEP_ID, EXCEL_ITEM, MAP_NO, BUILDING_NO, PRIMARY_SIDE, PRIMARY_SIDE_NAME, " +
                    "SECONDARY_SIDE, SECONDARY_SIDE_NAME, WIRE_QTY_SET, WIRE_SET_CNT, WIRE_LENGTH, WIRE_TOTAL_LENGTH, GROUND_WIRE_QTY, GROUND_WIRE_TOTAL_LENGTH, " +
                    "PIPE_LENGTH, PIPE_SET, PIPE_TOTAL_LENGTH, CREATE_DATE, CREATE_ID, (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID=pep.PIPE_NAME) " +
                    "AS project_item_id,  (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = pep.WIRE_NAME) AS WIRE_DESC, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = pep.GROUND_WIRE_NAME) AS GROUND_WIRE_DESC, PEP.PROJECT_ITEM_ID A " +
                    "FROM TND_MAP_PEP pep WHERE PROJECT_ID = @projectid",
                new SqlParameter("projectid", projectid)).ToList();
            }
            return lstPEP;
        }
        //取得弱電管線圖算資料
        public List<MAP_LCP_VIEW> getMapLCPById(string projectid)
        {
            log.Info("get map LCP info by projectid=" + projectid);
            List<MAP_LCP_VIEW> lstLCP = new List<MAP_LCP_VIEW>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstLCP = context.Database.SqlQuery<MAP_LCP_VIEW>("SELECT LCP_ID, EXCEL_ITEM, MAP_NO, BUILDING_NO, PRIMARY_SIDE, PRIMARY_SIDE_NAME, SECONDARY_SIDE, " +
                    "SECONDARY_SIDE_NAME, WIRE_QTY_SET, WIRE_SET_CNT, WIRE_LENGTH, WIRE_TOTAL_LENGTH, GROUND_WIRE_QTY, GROUND_WIRE_TOTAL_LENGTH, " +
                    "PIPE_1_LENGTH, PIPE_1_SET, PIPE_1_TOTAL_LEN, PIPE_2_LENGTH, PIPE_2_SET, PIPE_2_TOTAL_LEN, CREATE_DATE, CREATE_ID, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID=lcp.PIPE_1_NAME) AS project_item_id, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = lcp.WIRE_NAME) AS WIRE_DESC, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = lcp.GROUND_WIRE_NAME) AS GROUND_WIRE_DESC, " +
                    "(SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID = lcp.PIPE_2_NAME) AS PIPE_2_DESC, LCP.PROJECT_ITEM_ID A　" +
    "FROM TND_MAP_LCP lcp WHERE PROJECT_ID = @projectid",
                new SqlParameter("projectid", projectid)).ToList();
            }
            return lstLCP;
        }
        //取得設備清單圖算資料
        #region 設備清單資料
        public List<TND_MAP_DEVICE> getMapDEVICEById(string projectid)
        {
            log.Info("get map DEVICE info by projectid=" + projectid);
            List<TND_MAP_DEVICE> lstDEVICE = new List<TND_MAP_DEVICE>();
            //將設備加入與得標標單資訊整合
            string sql = @"SELECT DEVIVE_ID, PROJECT_ID, MAP_NO, BUILDING_NO, PROJECT_ITEM_ID, CREATE_DATE, CREATE_ID, QTY, 
                ISNULL(
                 (SELECT ITEM_DESC FROM TND_PROJECT_ITEM tpi WHERE tpi.PROJECT_ITEM_ID=device.PROJECT_ITEM_ID),
                 '___' + (SELECT ITEM_DESC FROM PLAN_ITEM tpi WHERE tpi.PLAN_ITEM_ID=device.PROJECT_ITEM_ID)) AS loc_desc,
                  DEVICE.LOC_DESC A 
                  FROM TND_MAP_DEVICE device WHERE PROJECT_ID =  @projectid ";
            log.Debug("sql=" + sql + ",projectId=" + projectid);
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstDEVICE = context.TND_MAP_DEVICE.SqlQuery(sql, new SqlParameter("projectid", projectid)).ToList();
            }
            return lstDEVICE;
        }
        #endregion
        //取得消防電修改資料
        #region 消防電資料
        TND_MAP_FP fp = null;
        public TND_MAP_FP getFPById(string id)
        {
            using (var context = new TopmepEntities())
            {
                fp = context.TND_MAP_FP.SqlQuery("select fp.* from TND_MAP_FP fp "
                    + "where fp.FP_ID = @id "
                   , new SqlParameter("id", id)).First();
            }
            return fp;
        }
        #endregion
        //取得消防水修改資料
        #region 消防水資料
        TND_MAP_FW fw = null;
        public TND_MAP_FW getFWById(string id)
        {
            using (var context = new TopmepEntities())
            {
                fw = context.TND_MAP_FW.SqlQuery("select fw.* from TND_MAP_FW fw "
                    + "where fw.FW_ID = @id "
                   , new SqlParameter("id", id)).First();
            }
            return fw;
        }
        #endregion
        //取得電氣管線修改資料
        #region 電氣管線資料
        TND_MAP_PEP pep = null;
        public TND_MAP_PEP getPEPById(string id)
        {
            using (var context = new TopmepEntities())
            {
                pep = context.TND_MAP_PEP.SqlQuery("select pep.* from TND_MAP_PEP pep "
                    + "where pep.PEP_ID = @id "
                   , new SqlParameter("id", id)).First();
            }
            return pep;
        }
        #endregion
        //取得弱電管線修改資料
        #region 弱電管線資料
        TND_MAP_LCP lcp = null;
        public TND_MAP_LCP getLCPById(string id)
        {
            using (var context = new TopmepEntities())
            {
                lcp = context.TND_MAP_LCP.SqlQuery("select lcp.* from TND_MAP_LCP lcp "
                    + "where lcp.LCP_ID = @id "
                   , new SqlParameter("id", id)).First();
            }
            return lcp;
        }
        #endregion
        //取得給排水修改資料
        #region 給排水資料
        TND_MAP_PLU plu = null;
        public TND_MAP_PLU getPLUById(string id)
        {
            using (var context = new TopmepEntities())
            {
                plu = context.TND_MAP_PLU.SqlQuery("select plu.* from TND_MAP_PLU plu "
                    + "where plu.PLU_ID = @id "
                   , new SqlParameter("id", id)).First();
            }
            return plu;
        }
        #endregion
        /// <summary>
        /// project item 基本資料
        /// </summary>
        /// <param name="itemid"></param>
        /// <returns></returns>
        public TND_PROJECT_ITEM getProjectItem(string itemid)
        {
            log.Debug("get project item by id=" + itemid);
            TND_PROJECT_ITEM pitem = null;
            using (var context = new TopmepEntities())
            {
                //條件篩選
                pitem = context.TND_PROJECT_ITEM.SqlQuery("SELECT * FROM TND_PROJECT_ITEM WHERE PROJECT_ITEM_ID=@itemid",
                new SqlParameter("itemid", itemid)).First();
            }
            return pitem;
        }
        //於現有品項下方新增一筆資料
        public int addProjectItemAfter(TND_PROJECT_ITEM item)
        {
            string sql = "UPDATE TND_PROJECT_ITEM SET EXCEL_ROW_ID=EXCEL_ROW_ID+1 WHERE PROJECT_ID = @projectid AND EXCEL_ROW_ID> @ExcelRowId ";

            using (var db = new TopmepEntities())
            {
                log.Debug("add exce rowid sql=" + sql + ",projectid=" + item.PROJECT_ID + ",ExcelRowI=" + item.EXCEL_ROW_ID);
                db.Database.ExecuteSqlCommand(sql, new SqlParameter("projectid", item.PROJECT_ID), new SqlParameter("ExcelRowId", item.EXCEL_ROW_ID));
            }
            item.PROJECT_ITEM_ID = "";
            item.EXCEL_ROW_ID = item.EXCEL_ROW_ID + 1;
            return updateProjectItem(item);
        }
        public int updateProjectItem(TND_PROJECT_ITEM item)
        {
            int i = 0;
            if (null == item.PROJECT_ITEM_ID || item.PROJECT_ITEM_ID == "")
            {
                log.Debug("add new project item in porjectid=" + item.PROJECT_ID);
                item = getNewProjectItemID(item);
            }
            log.Debug("project item key=" + item.PROJECT_ITEM_ID);
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.TND_PROJECT_ITEM.AddOrUpdate(item);
                    i = context.SaveChanges();
                }
                catch (Exception e)
                {
                    log.Error("updateProjectItem  fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }
            }
            return i;
        }
        //將Project Item 註記刪除
        public int changeProjectItem(string itemid, string delFlag)
        {
            string sql = "UPDATE TND_PROJECT_ITEM SET DEL_FLAG=@delFlag WHERE PROJECT_ITEM_ID = @itemid";
            int i = 0;
            using (var db = new TopmepEntities())
            {
                var parameters = new List<SqlParameter>();
                parameters.Add(new SqlParameter("itemid", itemid));
                parameters.Add(new SqlParameter("delFlag", delFlag));
                log.Info("Update Project_ITEM FLAG=" + sql + ",itemid=" + itemid + ",delFlag=" + delFlag);
                i = db.Database.ExecuteSqlCommand(sql, parameters.ToArray());
            }
            return i;
        }
        private TND_PROJECT_ITEM getNewProjectItemID(TND_PROJECT_ITEM item)
        {
            string sql = "SELECT MAX(CAST(SUBSTRING(PROJECT_ITEM_ID,8,LEN(PROJECT_ITEM_ID)) AS INT) +1) MaxSN, MAX(EXCEL_ROW_ID) + 1 as Row "
                + " FROM TND_PROJECT_ITEM WHERE PROJECT_ID = @projectid ; ";
            var parameters = new Dictionary<string, Object>();
            parameters.Add("projectid", item.PROJECT_ID);
            DataSet ds = ExecuteStoreQuery(sql, CommandType.Text, parameters);
            log.Debug("sql=" + sql + "," + ds.Tables[0].Rows[0][0].ToString() + "," + ds.Tables[0].Rows[0][1].ToString());
            int longMaxExcel = 1;
            int longMaxItem = 1;
            if (DBNull.Value != ds.Tables[0].Rows[0][0])
            {
                longMaxItem = int.Parse(ds.Tables[0].Rows[0][0].ToString());
                longMaxExcel = int.Parse(ds.Tables[0].Rows[0][1].ToString());
            }
            log.Debug("new project item id=" + longMaxItem + ",ExcelRowID=" + longMaxExcel);
            item.PROJECT_ITEM_ID = item.PROJECT_ID + "-" + longMaxItem;
            //新品項不會有Excel Row_id
            if (null == item.EXCEL_ROW_ID || item.EXCEL_ROW_ID == 0)
            {
                item.EXCEL_ROW_ID = longMaxExcel;
            }
            return item;
        }
    }

    //工率相關資料提供作業
    public class WageTableService : TnderProjectService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public TND_PROJECT wageTable = null;
        public List<PROJECT_ITEM_WITH_WAGE> wageTableItem = null;
        public TND_PROJECT name = null;
        public WageTableService()
        {
        }
        //取得工率表單
        public void getProjectId(string projectid)
        {
            log.Info("get project : projectid=" + projectid);
            using (var context = new TopmepEntities())
            {
                //取得工率表單檔頭資訊
                wageTable = context.TND_PROJECT.SqlQuery("SELECT * FROM TND_PROJECT WHERE PROJECT_ID=@projectid", new SqlParameter("projectid", projectid)).First();
                //取得工率表單明細
                wageTableItem = context.Database.SqlQuery<PROJECT_ITEM_WITH_WAGE>("SELECT i.*,w.ratio,w.price,map.QTY as MAP_QTY FROM TND_PROJECT_ITEM i LEFT OUTER JOIN "
                    + "TND_WAGE w ON i.PROJECT_ITEM_ID = w.PROJECT_ITEM_ID "
                    + "LEFT OUTER JOIN vw_MAP_MATERLIALIST map ON i.PROJECT_ITEM_ID = map.PROJECT_ITEM_ID "
                    + "WHERE i.project_id = @projectid AND ISNULL(i.DEL_FLAG,'N')='N' ORDER BY i.EXCEL_ROW_ID; ", new SqlParameter("projectid", projectid)).ToList();
                log.Debug("get project item count:" + wageTableItem.Count);
            }
        }
        #region 工率數量  
        //工率上傳數量  
        public int refreshWage(List<TND_WAGE> items)
        {
            //1.檢查專案是否存在
            //if (null == project) { throw new Exception("Project is not exist !!"); } 先註解掉,因為讀取不到project,會造成null == project is true,
            //而導致錯誤, 因為已設定是直接由專案頁面導入上傳圖算畫面，故不會有專案不存在的bug
            int i = 0;
            log.Info("refreshProjectItem = " + items.Count);
            //2.將Excel 資料寫入 
            using (var context = new TopmepEntities())
            {
                foreach (TND_WAGE item in items)
                {
                    //item.PROJECT_ID = project.PROJECT_ID;先註解掉,因為專案編號一開始已經設定了，會直接代入
                    context.TND_WAGE.Add(item);
                }
                i = context.SaveChanges();
            }
            log.Info("add TND_WAGE count =" + i);
            return i;
        }
        public int delWageByProject(string projectid)
        {
            log.Info("remove all wage by project ID=" + projectid);
            int i = 0;
            using (var context = new TopmepEntities())
            {
                log.Info("delete all TND_WAGE by proejct id=" + projectid);
                i = context.Database.ExecuteSqlCommand("DELETE FROM TND_WAGE WHERE PROJECT_ID=@projectid", new SqlParameter("@projectid", projectid));
            }
            log.Debug("delete TND_WAGE count=" + i);
            return i;
        }
        #endregion
        //取得工率資料
        public List<TND_WAGE> getWageById(string id)
        {
            log.Info("get wage ratio by projectid=" + id);
            List<TND_WAGE> lstWage = new List<TND_WAGE>();
            using (var context = new TopmepEntities())
            {
                //條件篩選
                lstWage = context.TND_WAGE.SqlQuery("SELECT * FROM TND_WAGE WHERE PROJECT_ID=@id",
                new SqlParameter("id", id)).ToList();
            }
            return lstWage;
        }

        public int updateWagePrice(string id)
        {
            int i = 0;
            log.Info("update wage price from wage multiplier by id :" + id);
            string sql = "UPDATE TND_WAGE SET TND_WAGE.PRICE = TND_WAGE.RATIO*tnd_project.WAGE_MULTIPLIER " +
                "from TND_WAGE left join tnd_project on TND_WAGE.PROJECT_ID = tnd_project.project_id " +
                "where TND_WAGE.PROJECT_ID = @projectid ";
            log.Debug("sql:" + sql);
            db = new TopmepEntities();
            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("projectid", id));
            db.Database.ExecuteSqlCommand(sql, parameters.ToArray());
            i = db.SaveChanges();
            log.Info("Update Record:" + i);
            db = null;
            return i;
        }
    }

    public class CostAnalysisDataService : WageTableService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public List<DirectCost> DirectCost4Project = null;
        public List<DirectCost> DirectCost4Budget = null;
        //直接成本
        public List<DirectCost> getDirectCost4Budget(string projectid)
        {
            List<DirectCost> lstDirecCost = null;
            using (var context = new TopmepEntities())
            {
                string sql = @"SELECT (select TYPE_CODE_1 + TYPE_CODE_2 from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE, 
                     (SELECT TYPE_DESC from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE_DESC ,
                     (SELECT SUB_TYPE_ID from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) T_SUB_CODE, 
                     TYPE_CODE_2 SUB_CODE, (select TYPE_DESC from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) SUB_DESC, 
                     SUM(tndQTY * tndPrice) MATERIAL_COST, SUM(MapQty * tndPrice) MATERIAL_COST_INMAP,
                     SUM(MapQty * RATIO) MAN_DAY_4EXCEL,
                     SUM(MapQty * RATIO * WagePrice) MAN_DAY_INMAP,
                     SUM(tndQTY * ITEM_UNIT_PRICE) CONTRACT_PRICE,
                     COUNT(*) ITEM_COUNT 
                     FROM(SELECT pi.*, w.RATIO, w.PRICE, it.ITEM_UNIT_PRICE tndPrice, it.ITEM_QUANTITY tndQTY, map.QTY MapQty, ISNULL(p.WAGE_MULTIPLIER, 0) AS WagePrice 
                     FROM PLAN_ITEM pi LEFT OUTER JOIN TND_WAGE w 
                     ON pi.PLAN_ITEM_ID = w.PROJECT_ITEM_ID LEFT OUTER JOIN vw_MAP_MATERLIALIST map 
                     ON pi.PLAN_ITEM_ID = map.PROJECT_ITEM_ID LEFT OUTER JOIN TND_PROJECT_ITEM it 
                     ON it.PROJECT_ITEM_ID = pi.PLAN_ITEM_ID LEFT JOIN TND_PROJECT p ON pi.PROJECT_ID = p.PROJECT_ID  
                     WHERE it.project_id =@projectid ) A 
                     GROUP BY TYPE_CODE_1, TYPE_CODE_2 ORDER BY ISNULL(TYPE_CODE_1,'無'), ISNULL(TYPE_CODE_2, '無') ;";
                log.Info("Get DirectCost SQL=" + sql + ",projectid=" + projectid);
                lstDirecCost = context.Database.SqlQuery<DirectCost>(sql, new SqlParameter("projectid", projectid)).ToList();

                log.Info("Get DirectCost Record Count=" + lstDirecCost.Count);
            }
            DirectCost4Budget = lstDirecCost;
            return DirectCost4Budget;
        }
        //預算參考表
        public List<DirectCost> getDirectCost4BudgetRef(string projectid)
        {
            List<DirectCost> lstDirecCost = null;
            using (var context = new TopmepEntities())
            {
                string sql = "SELECT (select TYPE_CODE_1 + TYPE_CODE_2 from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE, "
                    + "(SELECT TYPE_DESC from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE_DESC ,"
                    + "(SELECT SUB_TYPE_ID from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) T_SUB_CODE, "
                    + "TYPE_CODE_2 SUB_CODE, (select TYPE_DESC from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) SUB_DESC, "
                    + "SUM(ITEM_QUANTITY * ITEM_UNIT_PRICE) MATERIAL_COST, SUM(MapQty * ITEM_UNIT_PRICE) MATERIAL_COST_INMAP,"
                    + "SUM(ITEM_QUANTITY * RATIO) MAN_DAY,"
                    + "SUM(MapQty * RATIO) MAN_DAY_INMAP,"
                    + "SUM(ITEM_QUANTITY * ITEM_UNIT_COST) CONTRACT_PRICE,"
                    + "COUNT(*) ITEM_COUNT "
                    + "FROM(SELECT it.*, w.RATIO, w.PRICE, it.ITEM_UNIT_PRICE ITEM_UNIT_COST, map.QTY MapQty FROM TND_PROJECT_ITEM it LEFT OUTER JOIN TND_WAGE w "
                    + "ON it.PROJECT_ITEM_ID = w.PROJECT_ITEM_ID LEFT OUTER JOIN vw_MAP_MATERLIALIST map "
                    + "ON it.PROJECT_ITEM_ID = map.PROJECT_ITEM_ID "
                    + "WHERE it.project_id =@projectid ) A "
                    + "GROUP BY TYPE_CODE_1, TYPE_CODE_2 ORDER BY ISNULL(TYPE_CODE_1,'無'), ISNULL(TYPE_CODE_2, '無') ;";
                log.Info("Get DirectCost for budget SQL=" + sql + ",projectid=" + projectid);
                lstDirecCost = context.Database.SqlQuery<DirectCost>(sql, new SqlParameter("projectid", projectid)).ToList();
                log.Info("Get DirectCost for budget Record Count=" + lstDirecCost.Count);
            }
            DirectCost4Budget = lstDirecCost;
            return DirectCost4Budget;
        }
        //直接成本
        public List<DirectCost> getDirectCost(string projectid)
        {
            List<DirectCost> lstDirecCost = null;
            using (var context = new TopmepEntities())
            {
                string sql = "SELECT (select TYPE_CODE_1 + TYPE_CODE_2 from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE, "
                    + "(SELECT TYPE_DESC from REF_TYPE_MAIN WHERE  TYPE_CODE_1 + TYPE_CODE_2 = A.TYPE_CODE_1) MAINCODE_DESC ,"
                    + "(SELECT SUB_TYPE_ID from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) T_SUB_CODE, "
                    + "TYPE_CODE_2 SUB_CODE, (select TYPE_DESC from REF_TYPE_SUB WHERE  A.TYPE_CODE_1 + A.TYPE_CODE_2 = SUB_TYPE_ID) SUB_DESC, "
                    + "SUM(ITEM_QUANTITY * ITEM_UNIT_PRICE) MATERIAL_COST, SUM(MapQty * ITEM_UNIT_PRICE) MATERIAL_COST_INMAP,"
                    + "SUM(ITEM_QUANTITY * RATIO) MAN_DAY,"
                    + "SUM(MapQty * RATIO) MAN_DAY_INMAP,"
                    + "COUNT(*) ITEM_COUNT "
                    + "FROM(SELECT it.*, w.RATIO, w.PRICE, map.QTY MapQty FROM TND_PROJECT_ITEM it LEFT OUTER JOIN TND_WAGE w "
                    + "ON it.PROJECT_ITEM_ID = w.PROJECT_ITEM_ID LEFT OUTER JOIN vw_MAP_MATERLIALIST map "
                    + "ON it.PROJECT_ITEM_ID = map.PROJECT_ITEM_ID "
                    + "WHERE ISNULL(it.DEL_FLAG,'N')='N' AND it.project_id =@projectid ) A "
                    + "GROUP BY TYPE_CODE_1, TYPE_CODE_2 ORDER BY TYPE_CODE_1,TYPE_CODE_2;";
                log.Info("Get DirectCost SQL=" + sql + ",projectid=" + projectid);
                lstDirecCost = context.Database.SqlQuery<DirectCost>(sql, new SqlParameter("projectid", projectid)).ToList();
                log.Info("Get DirectCost Record Count=" + lstDirecCost.Count);
            }
            DirectCost4Project = lstDirecCost;
            return DirectCost4Project;
        }
        public List<SystemCost> getSystemCost(string projectid)
        {
            List<SystemCost> lstSystemCost = null;
            using (var context = new TopmepEntities())
            {
                string sql = "SELECT SYSTEM_MAIN,SYSTEM_SUB,"
                    + "SUM(ITEM_QUANTITY * ITEM_UNIT_PRICE) MATERIAL_COST, SUM(ITEM_QUANTITY * RATIO) MAN_DAY, "
                    + "SUM(MAP_QTY * ITEM_UNIT_PRICE) MATERIAL_COST_INMAP,SUM(MAP_QTY * RATIO) MAN_DAY, "
                    + "COUNT(*) ITEM_COUNT "
                    + "FROM(SELECT it.*, w.RATIO, w.PRICE, map.QTY MAP_QTY FROM TND_PROJECT_ITEM it LEFT OUTER JOIN TND_WAGE w "
                    + "ON it.PROJECT_ITEM_ID = w.PROJECT_ITEM_ID LEFT OUTER JOIN vw_MAP_MATERLIALIST map "
                    + "ON it.PROJECT_ITEM_ID = map.PROJECT_ITEM_ID "
                    + "WHERE ISNULL(it.DEL_FLAG,'N')='N' AND it.project_id =@projectid ) A "
                    + "GROUP BY SYSTEM_MAIN, SYSTEM_SUB ORDER BY SYSTEM_MAIN, SYSTEM_SUB;";
                log.Debug("Get SystemCost sql=" + sql);
                lstSystemCost = context.Database.SqlQuery<SystemCost>(sql, new SqlParameter("projectid", projectid)).ToList();
                log.Info("Get SystemCost Record Count=" + lstSystemCost.Count);
            }
            return lstSystemCost;
        }
    }
    //詢價單資料提供作業
    public class InquiryFormService : TnderProjectService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public TND_PROJECT_FORM formInquiry = null;
        public List<TND_PROJECT_FORM_ITEM> formInquiryItem = null;
        public Dictionary<string, COMPARASION_DATA> dirSupplierQuo = null;
        public string message = "";
        //取得詢價單
        public void getInqueryForm(string formid)
        {
            log.Info("get form : formid=" + formid);
            using (var context = new TopmepEntities())
            {
                //取得詢價單檔頭資訊
                string sql = "SELECT FORM_ID,PROJECT_ID,FORM_NAME,OWNER_NAME,OWNER_TEL "
                    + ",OWNER_EMAIL, OWNER_FAX, SUPPLIER_ID, CONTACT_NAME, CONTACT_EMAIL "
                    + ",DUEDATE, REF_ID, CREATE_ID, CREATE_DATE, MODIFY_ID"
                    + ",MODIFY_DATE,ISNULL(STATUS,'有效') as STATUS,ISNULL(ISWAGE,'N') as ISWAGE "
                    + "FROM TND_PROJECT_FORM WHERE FORM_ID = @formid";
                formInquiry = context.TND_PROJECT_FORM.SqlQuery(sql, new SqlParameter("formid", formid)).First();
                //取得詢價單明細
                formInquiryItem = context.TND_PROJECT_FORM_ITEM.SqlQuery("SELECT * FROM TND_PROJECT_FORM_ITEM WHERE FORM_ID=@formid  ORDER BY CAST(SUBSTRING(PROJECT_ITEM_ID,CHARINDEX('-',PROJECT_ITEM_ID)+1,5) as int)"
                    , new SqlParameter("formid", formid)).ToList();
                log.Debug("get form item count:" + formInquiryItem.Count);
            }
        }
        //取得主系統選單
        public List<string> getSystemMain(string projectid)
        {
            List<string> lst = new List<string>();
            using (var context = new TopmepEntities())
            {
                //取得主系統選單
                lst = context.Database.SqlQuery<string>("SELECT DISTINCT SYSTEM_MAIN FROM TND_PROJECT_ITEM　WHERE PROJECT_ID=@projectid;", new SqlParameter("projectid", projectid)).ToList();
                log.Info("Get System Main Count=" + lst.Count);
            }
            return lst;
        }
        //取得次系統選單
        public List<string> getSystemSub(string projectid)
        {
            List<string> lst = new List<string>();
            using (var context = new TopmepEntities())
            {
                //取得主系統選單
                lst = context.Database.SqlQuery<string>("SELECT DISTINCT SYSTEM_SUB FROM TND_PROJECT_ITEM WHERE PROJECT_ID=@projectid;", new SqlParameter("projectid", projectid)).ToList();
                //lst = context.TND_PROJECT_ITEM.SqlQuery("SELECT DISTINCT SYSTEM_SUB FROM TND_PROJECT_ITEM　WHERE PROJECT_ID=@projectid;", new SqlParameter("projectid", projectid)).ToList();
                log.Info("Get System Sub Count=" + lst.Count);
            }
            return lst;
        }
        //取得供應商選單
        public List<string> getSupplier()
        {
            List<string> lst = new List<string>();
            using (var context = new TopmepEntities())
            {
                //取得供應商選單
                lst = context.Database.SqlQuery<string>("SELECT (SELECT SUPPLIER_ID + '' + COMPANY_NAME FROM TND_SUPPLIER s2 WHERE s2.SUPPLIER_ID = s1.SUPPLIER_ID for XML PATH('')) AS suppliers FROM TND_SUPPLIER s1 ;").ToList();
                log.Info("Get Supplier Count=" + lst.Count);
            }
            return lst;
        }
        //取得特定專案報價之供應商資料
        public List<COMPARASION_DATA> getComparisonData(string projectid, string typecode1, string typecode2, string systemMain, string systemSub, string iswage, string formName)
        {
            //加入Project Item DEL FLG
            List<COMPARASION_DATA> lst = new List<COMPARASION_DATA>();
            string sql = "SELECT  pfItem.FORM_ID AS FORM_ID, " +
                "SUPPLIER_ID as SUPPLIER_NAME, FORM_NAME AS FORM_NAME,SUM(isNull(pfitem.ITEM_UNIT_PRICE,0) * isNull(pfitem.ITEM_QTY,0)) as TAmount " +
                "FROM TND_PROJECT_ITEM pItem LEFT OUTER JOIN " +
                "TND_PROJECT_FORM_ITEM pfItem ON pItem.PROJECT_ITEM_ID = pfItem.PROJECT_ITEM_ID " +
                "inner join TND_PROJECT_FORM f on pfItem.FORM_ID = f.FORM_ID " +
                "WHERE pItem.PROJECT_ID = @projectid AND ISNULL(pItem.DEL_FLAG,'N')='N' AND SUPPLIER_ID is not null AND ISNULL(f.STATUS,'有效')='有效' AND ISNULL(f.ISWAGE,'N')=@iswage ";
            var parameters = new List<SqlParameter>();
            //設定專案名編號資料
            parameters.Add(new SqlParameter("projectid", projectid));
            //設定報價單條件，材料或工資
            parameters.Add(new SqlParameter("iswage", iswage));
            //九宮格條件
            if (null != typecode1 && "" != typecode1)
            {
                //sql = sql + " AND pItem.TYPE_CODE_1='"+ typecode1 + "'";
                sql = sql + " AND pItem.TYPE_CODE_1=@typecode1";
                parameters.Add(new SqlParameter("typecode1", typecode1));
            }
            //次九宮格條件
            if (null != typecode2 && "" != typecode2)
            {
                //sql = sql + " AND pItem.TYPE_CODE_2='" + typecode2 + "'";
                sql = sql + " AND pItem.TYPE_CODE_2=@typecode2";
                parameters.Add(new SqlParameter("typecode2", typecode2));
            }
            //主系統條件
            if (null != systemMain && "" != systemMain)
            {
                // sql = sql + " AND pItem.SYSTEM_MAIN='" + systemMain + "'";
                sql = sql + " AND pItem.SYSTEM_MAIN=@systemMain";
                parameters.Add(new SqlParameter("systemMain", systemMain));
            }
            //次系統條件
            if (null != systemSub && "" != systemSub)
            {
                //sql = sql + " AND pItem.SYSTEM_SUB='" + systemSub + "'";
                sql = sql + " AND pItem.SYSTEM_SUB=@systemSub";
                parameters.Add(new SqlParameter("systemSub", systemSub));
            }
            //採購名稱條件
            if (null != formName && "" != formName)
            {
                //sql = sql + " AND f.FORM_NAME='" + formName + "'";
                sql = sql + " AND FORM_NAME = @formName ";
                parameters.Add(new SqlParameter("formName", formName));
            }
            sql = sql + " GROUP BY pfItem.FORM_ID ,SUPPLIER_ID, FORM_NAME ;";
            log.Info("comparison data sql=" + sql);
            using (var context = new TopmepEntities())
            {
                //取得主系統選單
                lst = context.Database.SqlQuery<COMPARASION_DATA>(sql, parameters.ToArray()).ToList();
                log.Info("Get ComparisonData Count=" + lst.Count);
            }
            return lst;
        }
        //比價資料
        public DataTable getComparisonDataToPivot(string projectid, string typecode1, string typecode2, string systemMain, string systemSub, string iswage, string formName)
        {
            // 還不確定要不要加入刪除的項目
            if (null != formName && "" != formName)
            {
                string sql = "SELECT * from (select pitem.EXCEL_ROW_ID 行數, pitem.PROJECT_ITEM_ID 代號,pitem.ITEM_ID 項次,pitem.ITEM_DESC 品項名稱,pitem.ITEM_UNIT 單位," +
                "(SELECT SUPPLIER_ID +'|' + fitem.FORM_ID + '|' + FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as SUPPLIER_NAME, " +
                "pitem.ITEM_UNIT_PRICE 單價, " +
                "(SELECT FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as FORM_NAME, fitem.ITEM_UNIT_PRICE " +
                "from TND_PROJECT_ITEM pitem " +
                "left join TND_PROJECT_FORM_ITEM fitem " +
                " on pitem.PROJECT_ITEM_ID = fitem.PROJECT_ITEM_ID " +
                "where pitem.PROJECT_ID = @projectid AND ISNULL(pitem.DEL_FLAG,'N')='N' ";

                if (iswage == "Y")
                {
                    sql = "SELECT * from (select pitem.EXCEL_ROW_ID 行數, pitem.PROJECT_ITEM_ID 代號,pitem.ITEM_ID 項次,pitem.ITEM_DESC 品項名稱,pitem.ITEM_UNIT 單位," +
                    "(SELECT SUPPLIER_ID+'|'+ fitem.FORM_ID + '|' + FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as SUPPLIER_NAME, " +
                    "pitem.MAN_PRICE 工資單價, " +
                    "(SELECT FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as FORM_NAME, fitem.ITEM_UNIT_PRICE " +
                    "from TND_PROJECT_ITEM pitem " +
                    "left join TND_PROJECT_FORM_ITEM fitem " +
                    " on pitem.PROJECT_ITEM_ID = fitem.PROJECT_ITEM_ID " +
                    "where pitem.PROJECT_ID = @projectid ISNULL(pitem.DEL_FLAG,'N')='N' ";
                }

                var parameters = new Dictionary<string, Object>();
                //設定專案名編號資料
                parameters.Add("projectid", projectid);
                //九宮格條件
                if (null != typecode1 && "" != typecode1)
                {
                    //sql = sql + " AND pItem.TYPE_CODE_1='"+ typecode1 + "'";
                    sql = sql + " AND pItem.TYPE_CODE_1=@typecode1";
                    parameters.Add("typecode1", typecode1);
                }
                //次九宮格條件
                if (null != typecode2 && "" != typecode2)
                {
                    //sql = sql + " AND pItem.TYPE_CODE_2='" + typecode2 + "'";
                    sql = sql + " AND pItem.TYPE_CODE_2=@typecode2";
                    parameters.Add("typecode2", typecode2);
                }
                //主系統條件
                if (null != systemMain && "" != systemMain)
                {
                    // sql = sql + " AND pItem.SYSTEM_MAIN='" + systemMain + "'";
                    sql = sql + " AND pItem.SYSTEM_MAIN=@systemMain";
                    parameters.Add("systemMain", systemMain);
                }
                //次系統條件
                if (null != systemSub && "" != systemSub)
                {
                    //sql = sql + " AND pItem.SYSTEM_SUB='" + systemSub + "'";
                    sql = sql + " AND pItem.SYSTEM_SUB=@systemSub";
                    parameters.Add("systemSub", systemSub);
                }
                //取的欄位維度條件
                List<COMPARASION_DATA> lstSuppluerQuo = getComparisonData(projectid, typecode1, typecode2, systemMain, systemSub, iswage, formName);
                if (lstSuppluerQuo.Count == 0)
                {
                    throw new Exception("相關條件沒有任何報價資料!!");
                }
                //設定供應商報價資料，供前端畫面調用
                dirSupplierQuo = new Dictionary<string, COMPARASION_DATA>();
                string dimString = "";
                foreach (var it in lstSuppluerQuo)
                {
                    log.Debug("Supplier=" + it.SUPPLIER_NAME + "," + it.FORM_ID + "," + it.FORM_NAME);
                    if (dimString == "")
                    {
                        dimString = "[" + it.SUPPLIER_NAME + "|" + it.FORM_ID + "|" + it.FORM_NAME + "]";
                    }
                    else
                    {
                        dimString = dimString + ",[" + it.SUPPLIER_NAME + "|" + it.FORM_ID + "|" + it.FORM_NAME + "]";
                    }
                    //設定供應商報價資料，供前端畫面調用
                    dirSupplierQuo.Add(it.FORM_ID, it);
                }

                log.Debug("dimString=" + dimString);
                //sql = sql + " AND FORM_NAME ='" + formName + "'";
                sql = sql + ") souce pivot(MIN(ITEM_UNIT_PRICE) FOR SUPPLIER_NAME IN(" + dimString + ")) as pvt WHERE FORM_NAME =@formName ORDER BY 行數; ";
                parameters.Add("formName", formName);
                log.Info("comparison data sql=" + sql);
                DataSet ds = ExecuteStoreQuery(sql, CommandType.Text, parameters);
                //Pivot pvt = new Pivot(ds.Tables[0]);
                return ds.Tables[0];
            }
            else
            {

                string sql = "SELECT * from (select pitem.EXCEL_ROW_ID 行數, pitem.PROJECT_ITEM_ID 代號,pitem.ITEM_ID 項次,pitem.ITEM_DESC 品項名稱,pitem.ITEM_UNIT 單位," +
                                "(SELECT SUPPLIER_ID+'|'+ fitem.FORM_ID + '|' + FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as SUPPLIER_NAME, " +
                                "pitem.ITEM_UNIT_PRICE 單價,fitem.ITEM_UNIT_PRICE " +
                                "from TND_PROJECT_ITEM pitem " +
                                "left join TND_PROJECT_FORM_ITEM fitem " +
                                " on pitem.PROJECT_ITEM_ID = fitem.PROJECT_ITEM_ID " +
                                "where pitem.PROJECT_ID = @projectid ";

                if (iswage == "Y")
                {
                    sql = "SELECT * from (select pitem.EXCEL_ROW_ID 行數, pitem.PROJECT_ITEM_ID 代號,pitem.ITEM_ID 項次,pitem.ITEM_DESC 品項名稱,pitem.ITEM_UNIT 單位," +
                    "(SELECT SUPPLIER_ID+'|'+ fitem.FORM_ID + '|' + FORM_NAME FROM TND_PROJECT_FORM f WHERE f.FORM_ID = fitem.FORM_ID AND ISNULL(f.STATUS,'有效')='有效') as SUPPLIER_NAME, " +
                    "pitem.MAN_PRICE 工資單價,fitem.ITEM_UNIT_PRICE " +
                    "from TND_PROJECT_ITEM pitem " +
                    "left join TND_PROJECT_FORM_ITEM fitem " +
                    " on pitem.PROJECT_ITEM_ID = fitem.PROJECT_ITEM_ID " +
                    "where pitem.PROJECT_ID = @projectid ";
                }

                var parameters = new Dictionary<string, Object>();
                //設定專案名編號資料
                parameters.Add("projectid", projectid);
                //九宮格條件
                if (null != typecode1 && "" != typecode1)
                {
                    //sql = sql + " AND pItem.TYPE_CODE_1='"+ typecode1 + "'";
                    sql = sql + " AND pItem.TYPE_CODE_1=@typecode1";
                    parameters.Add("typecode1", typecode1);
                }
                //次九宮格條件
                if (null != typecode2 && "" != typecode2)
                {
                    //sql = sql + " AND pItem.TYPE_CODE_2='" + typecode2 + "'";
                    sql = sql + " AND pItem.TYPE_CODE_2=@typecode2";
                    parameters.Add("typecode2", typecode2);
                }
                //主系統條件
                if (null != systemMain && "" != systemMain)
                {
                    // sql = sql + " AND pItem.SYSTEM_MAIN='" + systemMain + "'";
                    sql = sql + " AND pItem.SYSTEM_MAIN=@systemMain";
                    parameters.Add("systemMain", systemMain);
                }
                //次系統條件
                if (null != systemSub && "" != systemSub)
                {
                    //sql = sql + " AND pItem.SYSTEM_SUB='" + systemSub + "'";
                    sql = sql + " AND pItem.SYSTEM_SUB=@systemSub";
                    parameters.Add("systemSub", systemSub);
                }
                //取的欄位維度條件
                List<COMPARASION_DATA> lstSuppluerQuo = getComparisonData(projectid, typecode1, typecode2, systemMain, systemSub, iswage, formName);
                if (lstSuppluerQuo.Count == 0)
                {
                    throw new Exception("相關條件沒有任何報價資料!!");
                }
                //設定供應商報價資料，供前端畫面調用
                dirSupplierQuo = new Dictionary<string, COMPARASION_DATA>();
                string dimString = "";
                foreach (var it in lstSuppluerQuo)
                {
                    log.Debug("Supplier=" + it.SUPPLIER_NAME + "," + it.FORM_ID + "," + it.FORM_NAME);
                    if (dimString == "")
                    {
                        dimString = "[" + it.SUPPLIER_NAME + "|" + it.FORM_ID + "|" + it.FORM_NAME + "]";
                    }
                    else
                    {
                        dimString = dimString + ",[" + it.SUPPLIER_NAME + "|" + it.FORM_ID + "|" + it.FORM_NAME + "]";
                    }
                    //設定供應商報價資料，供前端畫面調用
                    dirSupplierQuo.Add(it.FORM_ID, it);
                }

                log.Debug("dimString=" + dimString);
                sql = sql + ") souce pivot(MIN(ITEM_UNIT_PRICE) FOR SUPPLIER_NAME IN(" + dimString + ")) as pvt ORDER BY 行數; ";
                log.Info("comparison data sql=" + sql);
                DataSet ds = ExecuteStoreQuery(sql, CommandType.Text, parameters);
                //Pivot pvt = new Pivot(ds.Tables[0]);
                return ds.Tables[0];
            }
        }
        //取得專案詢價單樣板(供應商欄位為0)
        public List<TND_PROJECT_FORM> getFormTemplateByProject(string projectid)
        {
            log.Info("get inquiry template by projectid=" + projectid);
            List<TND_PROJECT_FORM> lst = new List<TND_PROJECT_FORM>();
            using (var context = new TopmepEntities())
            {
                //取得詢價單樣本資訊
                string sql = "SELECT FORM_ID,PROJECT_ID,FORM_NAME,OWNER_NAME,OWNER_TEL,OWNER_EMAIL "
                    + ",OWNER_FAX,SUPPLIER_ID,CONTACT_NAME,CONTACT_EMAIL,DUEDATE,REF_ID,CREATE_ID,CREATE_DATE "
                    + ",MODIFY_ID,MODIFY_DATE,ISNULL(STATUS,'有效') STATUS, ISNULL(ISWAGE,'N') ISWAGE "
                    + "FROM TND_PROJECT_FORM WHERE SUPPLIER_ID IS NULL AND PROJECT_ID =@projectid ORDER BY FORM_ID DESC";
                lst = context.TND_PROJECT_FORM.SqlQuery(sql, new SqlParameter("projectid", projectid)).ToList();
            }
            return lst;
        }
        public List<SupplierFormFunction> getFormByProject(string projectid, string _status)
        {
            List<SupplierFormFunction> lst = new List<SupplierFormFunction>();
            string status = "有效";
            if (null != _status && _status != "*")
            {
                status = _status;
            }
            using (var context = new TopmepEntities())
            {
                string sql = "SELECT a.FORM_ID, a.SUPPLIER_ID, a.FORM_NAME, SUM(b.ITEM_QTY*b.ITEM_UNIT_PRICE) AS TOTAL_PRICE, "
                    + "ROW_NUMBER() OVER(ORDER BY a.FORM_ID DESC) AS NO, ISNULL(A.STATUS, '有效') AS STATUS,ISNULL(A.ISWAGE,'N') ISWAGE "
                    + "FROM TND_PROJECT_FORM a left JOIN TND_PROJECT_FORM_ITEM b ON a.FORM_ID = b.FORM_ID "
                    + "WHERE ISNULL(A.STATUS,'有效')=@status "
                    + "GROUP BY a.FORM_ID, a.SUPPLIER_ID, a.FORM_NAME, a.PROJECT_ID,a.STATUS,a.ISWAGE "
                    + "HAVING  a.SUPPLIER_ID IS NOT NULL "
                    + "AND a.PROJECT_ID =@projectid ORDER BY a.FORM_ID DESC, a.FORM_NAME ";
                lst = context.Database.SqlQuery<SupplierFormFunction>(sql, new SqlParameter("status", status), new SqlParameter("projectid", projectid)).ToList();
            }
            log.Info("get function count:" + lst.Count);
            return lst;
        }
        public int createInquiryFormFromSupplier(TND_PROJECT_FORM form, List<TND_PROJECT_FORM_ITEM> items)
        {
            int i = 0;
            //1.建立詢價單價單樣本
            string sno_key = "PO";
            SerialKeyService snoservice = new SerialKeyService();
            form.FORM_ID = snoservice.getSerialKey(sno_key);
            log.Info("Inquiry form from supplier =" + form.ToString());
            using (var context = new TopmepEntities())
            {
                context.TND_PROJECT_FORM.Add(form);

                log.Info("project form id = " + form.FORM_ID + ",project form item conunt=" + items.Count);
                //if (i > 0) { status = true; };
                foreach (TND_PROJECT_FORM_ITEM item in items)
                {
                    item.FORM_ID = form.FORM_ID;
                    context.TND_PROJECT_FORM_ITEM.Add(item);
                    log.Debug("TND_PROJECT_FORM_ITEM:" + item.FORM_ID + ",project_item_id=" + item.PROJECT_ITEM_ID);
                }
                try
                {
                    i = context.SaveChanges();
                }
                catch (Exception ex)
                {
                    log.Error(ex.StackTrace);
                    return -1;
                }
            }
            return i;
        }
        //由報價單資料更新標單資料
        public int updateCostFromQuote(string projectItemid, decimal price, string iswage)
        {
            int i = 0;
            log.Info("Update Cost:project item id=" + projectItemid + ",price=" + price);
            db = new TopmepEntities();
            string sql = "UPDATE TND_PROJECT_ITEM SET ITEM_UNIT_PRICE=@price WHERE PROJECT_ITEM_ID=@pitemid ";
            //將工資報價單更新工資報價欄位
            if (iswage == "Y")
            {
                sql = "UPDATE TND_PROJECT_ITEM SET MAN_PRICE=@price WHERE PROJECT_ITEM_ID=@pitemid ";
            }
            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("price", price));
            parameters.Add(new SqlParameter("pitemid", projectItemid));
            db.Database.ExecuteSqlCommand(sql, parameters.ToArray());
            i = db.SaveChanges();
            db = null;
            log.Info("Update Cost:" + i);
            return i;
        }
        public int batchUpdateCostFromQuote(string formid, string iswage)
        {
            int i = 0;
            log.Info("Copy cost from Quote to Tnd by form id" + formid);
            string sql = "UPDATE  TND_PROJECT_ITEM SET item_unit_price = i.ITEM_UNIT_PRICE "
                + "FROM (select i.project_item_id, fi.ITEM_UNIT_PRICE, fi.FORM_ID from TND_PROJECT_ITEM i "
                + ", TND_PROJECT_FORM_ITEM fi "
                + "where i.PROJECT_ITEM_ID = fi.PROJECT_ITEM_ID and FORM_ID = @formid) i "
                + "WHERE  i.project_item_id = TND_PROJECT_ITEM.PROJECT_ITEM_ID";
            //將工資報價單更新工資報價欄位
            if (iswage == "Y")
            {
                sql = "UPDATE  TND_PROJECT_ITEM SET MAN_PRICE = i.ITEM_UNIT_PRICE "
                + "FROM (select i.project_item_id, fi.ITEM_UNIT_PRICE, fi.FORM_ID from TND_PROJECT_ITEM i "
                + ", TND_PROJECT_FORM_ITEM fi "
                + "where i.PROJECT_ITEM_ID = fi.PROJECT_ITEM_ID and FORM_ID = @formid) i "
                + "WHERE  i.project_item_id = TND_PROJECT_ITEM.PROJECT_ITEM_ID";
            }
            log.Debug("batch sql:" + sql);
            db = new TopmepEntities();
            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("formid", formid));
            db.Database.ExecuteSqlCommand(sql, parameters.ToArray());
            i = db.SaveChanges();
            log.Info("Update Record:" + i);
            db = null;
            return i;
        }
        /// <summary>
        /// 註銷或回復備標階段詢價單
        /// </summary>
        /// <param name="formid"></param>
        /// <param name="status"></param>
        /// <returns></returns>
        public int changeProjectFormStatus(string formid, string status)
        {
            int i = 0;
            log.Info("Update project form status formid=" + formid + ",status=" + status);
            db = new TopmepEntities();
            string sql = "UPDATE TND_PROJECT_FORM SET STATUS=@status WHERE FORM_ID=@formid ";
            var parameters = new List<SqlParameter>();
            parameters.Add(new SqlParameter("status", status));
            parameters.Add(new SqlParameter("formid", formid));
            db.Database.ExecuteSqlCommand(sql, parameters.ToArray());
            i = db.SaveChanges();
            db = null;
            log.Debug("Update project form status  :" + i);
            return i;
        }
        public string zipAllTemplate4Download(string projectid)
        {
            //1.取得專案所有空白詢價單
            List<TND_PROJECT_FORM> lstTemplate = getFormTemplateByProject(projectid);
            ZipFileCreator zipTool = new ZipFileCreator();
            //2.設定暫存目錄
            string tempFolder = ContextService.strUploadPath + "\\" + projectid + "\\" + ContextService.quotesFolder + "\\Temp\\";
            ZipFileCreator.DelDirectory(tempFolder);
            ZipFileCreator.CreateDirectory(tempFolder);
            //3.批次產生空白詢價單
            InquiryFormToExcel poi = new InquiryFormToExcel();
            TND_PROJECT p = getProjectById(projectid);
            foreach (TND_PROJECT_FORM f in lstTemplate)
            {
                getInqueryForm(f.FORM_ID);
                ZipFileCreator.CreateDirectory(tempFolder + formInquiry.FORM_NAME);
                string fileLocation = poi.exportExcel(formInquiry, formInquiryItem, true);
                log.Debug("temp file=" + fileLocation);
            }
            //4.Zip all file
            return zipTool.ZipDirectory(tempFolder);
            //return zipTool.ZipFiles(tempFolder, null, p.PROJECT_NAME);
        }
    }
    #endregion

    #region 供應商管理區塊
    /*
     *供應商管理 
     */
    public class SupplierManage : ContextService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public TND_SUPPLIER supplier = null;
        //  public SUP_MATERIAL_RELATION typemain = null;
        public TND_SUP_CONTACT_INFO contact = null;
        public List<TND_SUP_CONTACT_INFO> contactList = null;
        string sno_key = "SUP";
        public string newSupplier(TND_SUPPLIER sup)
        {
            //1.建立供應商基本資料
            log.Info("create new supplier ");
            supplier = sup;
            // typemain = sm;
            //contact = sc;
            using (var context = new TopmepEntities())
            {
                //2.取得供應商編號
                SerialKeyService snoservice = new SerialKeyService();
                supplier.SUPPLIER_ID = snoservice.getSerialKey(sno_key);
                log.Info("new supplier object=" + supplier.ToString());
                context.TND_SUPPLIER.Add(supplier);
                int i = context.SaveChanges();
                log.Debug("Add supplier=" + i);
                //sm.SUPPLIER_ID = supplier.SUPPLIER_ID;
                //context.SUP_MATERIAL_RELATION.Add(typemain);
                //int j = context.SaveChanges();
                //log.Debug("Add typemain=" + j);
                //sc.SUPPLIER_MATERIAL_ID = supplier.SUPPLIER_ID;
                //context.TND_SUP_CONTACT_INFO.Add(contact);
                //int k = context.SaveChanges();
                //log.Debug("Add contact=" + k);
                //if (i > 0) { status = true; };
            }
            return supplier.SUPPLIER_ID;
        }

        public TND_SUPPLIER getSupplierById(string supid)
        {
            using (var context = new TopmepEntities())
            {
                supplier = context.TND_SUPPLIER.SqlQuery("select s.* from TND_SUPPLIER s "
                    + "where s.SUPPLIER_ID = @sid "
                   , new SqlParameter("sid", supid)).First();
            }
            return supplier;
        }
        public void updateSupplier(TND_SUPPLIER sup)
        {
            //1.更新供應商基本資料
            supplier = sup;
            log.Info("Update supplier information");
            using (var context = new TopmepEntities())
            {
                context.Entry(supplier).State = EntityState.Modified;
                int i = context.SaveChanges();
                log.Debug("Update Supplier=" + i);
            }
        }
        //取得供應商九宮格資料
        //TND_SUP_MATERIAL_RELATION sm= null;
        //public TND_SUP_MATERIAL_RELATION getTypeMainById(string id)
        //{
        //   using (var context = new TopmepEntities())
        //    {
        //        sm = context.TND_SUP_MATERIAL_RELATION.SqlQuery("select sm.* from TND_SUP_MATERIAL_RELATION sm "
        //            + "where sm.RELATION_ID = @id "
        //           , new SqlParameter("id", id)).First();
        //    }
        //    return sm;
        //}
        //public int updateTypeMain(TND_SUP_MATERIAL_RELATION sm)
        //{
        //更新供應商九宮格資料
        //   log.Info("Update typemain " +sm.SUPPLIER_ID + "," + sm.ToString());
        //    int i = 0;
        //    using (var context = new TopmepEntities())
        //    {
        //        context.TND_SUP_MATERIAL_RELATION.AddOrUpdate(sm);
        //        i = context.SaveChanges();
        //        log.Debug("Update typemain=" + i);
        //    }
        //    return i;
        //}

        //public string message = "";
        //public int refreshTypeMain(TND_SUP_MATERIAL_RELATION item)
        //{
        //    int i = 0;
        //   using (var context = new TopmepEntities())
        //    {
        //        try
        //        {
        //            context.TND_SUP_MATERIAL_RELATION.AddOrUpdate(item);
        //            i = context.SaveChanges();
        //        }
        //        catch (Exception e)
        //        {
        //            log.Error("update supplier type main  fail:" + e.ToString());
        //            log.Error(e.StackTrace);
        //            message = e.Message;
        //        }
        //
        //    }
        //    return i;
        //}


        //取得供應商資料
        public TND_SUPPLIER getContactById(string supid)
        {
            using (var context = new TopmepEntities())
            {
                supplier = context.TND_SUPPLIER.SqlQuery("select sup.* from TND_SUPPLIER sup "
                    + "where sup.SUPPLIER_ID = @supid "
                   , new SqlParameter("supid", supid)).First();
            }
            return supplier;
        }

        //取得供應商聯絡人資料
        public List<TND_SUP_CONTACT_INFO> getContactBySupplier(string supid)
        {
            List<TND_SUP_CONTACT_INFO> contactors = null;
            using (var context = new TopmepEntities())
            {
                contactors = context.TND_SUP_CONTACT_INFO.SqlQuery("select sup.* from TND_SUP_CONTACT_INFO sup "
                    + "where sup.SUPPLIER_MATERIAL_ID = @supid "
                   , new SqlParameter("supid", supid)).ToList();
                log.Info("contacs:" + contactors.Count);
            }
            return contactors;
        }
        public int updateContact(TND_SUP_CONTACT_INFO sc)
        {
            //更新供應商聯絡人資料
            log.Info("Update contact " + sc.SUPPLIER_MATERIAL_ID + "," + sc.ToString());
            int i = 0;
            using (var context = new TopmepEntities())
            {
                context.TND_SUP_CONTACT_INFO.AddOrUpdate(sc);
                i = context.SaveChanges();
                log.Debug("Update contact=" + i);
            }
            return i;
        }
        public string message = "";
        public int refreshContact(TND_SUP_CONTACT_INFO item)
        {
            int i = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.TND_SUP_CONTACT_INFO.AddOrUpdate(item);
                    i = context.SaveChanges();
                }
                catch (Exception e)
                {
                    log.Error("update supplier contact fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return i;
        }
        //取得供應商
        public void getSupplierBySupId(string id)
        {
            log.Info("get form : supplier id=" + id);
            using (var context = new TopmepEntities())
            {
                //取得供應商檔頭資訊
                string sql = "SELECT * FROM TND_SUPPLIER sup WHERE sup.SUPPLIER_ID =@id ";
                supplier = context.TND_SUPPLIER.SqlQuery(sql, new SqlParameter("id", id)).FirstOrDefault();
                //取得聯絡人明細
                contactList = context.TND_SUP_CONTACT_INFO.SqlQuery("SELECT * FROM TND_SUP_CONTACT_INFO cnt WHERE cnt.SUPPLIER_MATERIAL_ID =@id", new SqlParameter("id", id)).ToList();
                log.Debug("get supplier contact count:" + contactList.Count);
            }
        }


        //更新供應與聯絡人資料
        public int updateSupplier(string supplierid, TND_SUPPLIER sup, List<TND_SUP_CONTACT_INFO> lstItem)
        {
            log.Info("Update supplier id =" + supplierid);
            supplier = sup;
            int i = 0;
            int j = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.Entry(supplier).State = EntityState.Modified;
                    i = context.SaveChanges();
                    log.Debug("Update Supplier =" + i);
                    log.Info("contact item = " + lstItem.Count);
                    //2.將item資料寫入 
                    foreach (TND_SUP_CONTACT_INFO item in lstItem)
                    {
                        TND_SUP_CONTACT_INFO existItem = null;
                        log.Debug("contact item id=" + item.CONTACT_ID);
                        if (item.CONTACT_ID != 0)
                        {
                            existItem = context.TND_SUP_CONTACT_INFO.Find(item.CONTACT_ID);
                        }
                        else
                        {
                            var parameters = new List<SqlParameter>();
                            parameters.Add(new SqlParameter("supplierid", supplierid));
                            parameters.Add(new SqlParameter("contactname", item.CONTACT_NAME));
                            string sql = "SELECT * FROM TND_SUP_CONTACT_INFO WHERE SUPPLIER_MATERIAL_ID=@supplierid AND CONTACT_NAME =@contactname";
                            log.Info(sql + " ;" + supplierid + ", contactname=" + item.CONTACT_NAME);
                            TND_SUP_CONTACT_INFO excelItem = context.TND_SUP_CONTACT_INFO.SqlQuery(sql, parameters.ToArray()).First();
                            existItem = context.TND_SUP_CONTACT_INFO.Find(excelItem.CONTACT_ID);

                        }
                        log.Debug("find exist item=" + existItem.CONTACT_ID);
                        existItem.CONTACT_NAME = item.CONTACT_NAME;
                        existItem.CONTACT_TEL = item.CONTACT_TEL;
                        existItem.CONTACT_FAX = item.CONTACT_FAX;
                        existItem.CONTACT_MOBIL = item.CONTACT_MOBIL;
                        existItem.CONTACT_EMAIL = item.CONTACT_EMAIL;
                        existItem.REMARK = item.REMARK;
                        context.TND_SUP_CONTACT_INFO.AddOrUpdate(existItem);
                    }
                    j = context.SaveChanges();
                    log.Debug("Update contact item =" + j);
                    if (i != 0)
                    {
                        return i;
                    }
                    else
                    {
                        return j;
                    }
                }
                catch (Exception e)
                {
                    log.Error("update new supplier id fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }

            }
            return i;
        }

        //更新供應資料
        public int updateOnlySupplier(string supplierid, TND_SUPPLIER sup)
        {
            log.Info("Update supplier id =" + supplierid);
            supplier = sup;
            int i = 0;
            using (var context = new TopmepEntities())
            {
                try
                {
                    context.Entry(supplier).State = EntityState.Modified;
                    i = context.SaveChanges();
                    log.Debug("Update Supplier =" + i);
                    if (i != 0)
                    {
                        return i;
                    }
                }
                catch (Exception e)
                {
                    log.Error("update new supplier id fail:" + e.ToString());
                    log.Error(e.StackTrace);
                    message = e.Message;
                }
            }
            return i;
        }
    }
    #endregion

    #region 檔案管理區塊
    /*
     *檔案管理 
     */
    public class FileManage : ContextService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        public string addFile(string projectid, string keyName, string fileName, string fileType, string path, string createId, string createDate)
        {
            int i = 0;
            //寫入檔案相關資料
            using (var context = new TopmepEntities())
            {
                string sql = "INSERT INTO TND_FILE (PROJECT_ID, FILE_UPLOAD_NAME, FILE_ACTURE_NAME, "
                    + "FILE_TYPE, FILE_LOCATIOM, CREATE_ID, CREATE_DATE) "
                    + "VALUES ('" + projectid + "', '" + keyName + "', '" + fileName + "', '" + fileType + "', '" + path + "', '" + createId + "', CONVERT(datetime, '" + createDate + "', 120)) ";
                log.Info("sql =" + sql);
                var parameters = new List<SqlParameter>();
                i = context.Database.ExecuteSqlCommand(sql);
                return keyName;
            }
        }
        //移除檔案資料
        public int delFile(long itemid)
        {
            int i = 0;
            //2.將檔案資料刪除
            using (var context = new TopmepEntities())
            {
                try
                {
                    string sql = "DELETE FROM TND_FILE WHERE FILE_ID=@itemUid;";
                    var parameters = new List<SqlParameter>();
                    parameters.Add(new SqlParameter("itemUid", itemid));
                    log.Debug("Delete TND_FILE:" + itemid);
                    context.Database.ExecuteSqlCommand(sql, parameters.ToArray());
                    i = context.SaveChanges();
                }
                catch (Exception ex)
                {
                    log.Error(ex.Message + ":" + ex.StackTrace);
                }
            }
            return 1;
        }
        TND_FILE f = null;
        public TND_FILE getFileByItemId(long itemid)
        {
            using (var context = new TopmepEntities())
            {
                f = context.TND_FILE.SqlQuery("SELECT * FROM TND_FILE WHERE FILE_ID = @itemid "
                   , new SqlParameter("itemid", itemid)).FirstOrDefault();
            }
            return f;
        }
    }
    #endregion
}