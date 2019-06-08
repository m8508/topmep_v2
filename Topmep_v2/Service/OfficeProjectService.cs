using System.Collections;
using java.util;
using net.sf.mpxj;
using System;
using System.Collections.Generic;
using Topmep.Models;
using NLog;
using System.Data.SqlClient;

namespace Topmep.Service
{
    public class OfficeProjectService : ContextService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        string project_id;
        List<PLAN_TASK> lstTask = null;
        public void convertProject(string projectId, string filepath)
        {
            project_id = projectId;
            net.sf.mpxj.mpp.MPPReader reader = new net.sf.mpxj.mpp.MPPReader();
            ProjectFile projectObj = reader.read(filepath);
            int i = 1;
            lstTask = new List<PLAN_TASK>();
            foreach (net.sf.mpxj.Task task in ToEnumerable(projectObj.getAllTasks()))
            {
                PLAN_TASK pt = new PLAN_TASK();
                pt.PROJECT_ID = projectId;

                pt.PRJ_UID = task.getUniqueID().intValue();
                pt.PRJ_ID = task.getID().intValue();
                pt.TASK_NAME = task.getName();

                DateTime dtStart = new DateTime();
                DateTime dtFinish = new DateTime();
                //ToString("yyyyMMddHHmmss")
                if (null != task.getStart())
                {
                    dtStart = new DateTime((task.getStart().getYear() + 1900), task.getStart().getMonth() + 1, task.getStart().getDate());
                    log.Debug("start date Year =" + (task.getStart().getYear() + 1900) + ",Month=" + (task.getStart().getMonth() + 1) + ",Date=" + task.getStart().getDate());
                    pt.START_DATE = dtStart;

                    dtFinish = new DateTime((task.getFinish().getYear() + 1900), task.getFinish().getMonth() + 1, task.getFinish().getDate());
                    pt.FINISH_DATE = dtFinish;
                    log.Debug("start date Year =" + (task.getFinish().getYear() + 1900) + ",Month=" + (task.getFinish().getMonth() + 1) + ",Date=" + task.getFinish().getDate());

                    pt.DURATION = task.getDuration().toString();
                }
                log.Debug("DURATION=" + task.getDuration() + ",Task: " + i + "=" + task.getName() + ",StartDate=" + dtStart.ToString("yyyy/MM/dd") + ",EndDate=" + dtFinish.ToString("yyyy/MM/dd") + " ID=" + task.getID() + " Unique ID=" + task.getUniqueID());
                if (null != task.getParentTask())
                {
                    pt.PARENT_UID = task.getParentTask().getUniqueID().intValue();
                }
                log.Info("Parent UID=" + pt.PARENT_UID + ",TASK_UID=" + pt.PRJ_UID + ",Task_id=" + pt.TASK_ID + ",TASK_NAME=" + pt.TASK_NAME);
                lstTask.Add(pt);
                i++;
            }
            log.Info("Get all task count:" + lstTask.Count);
        }
        public void import2Table()
        {
            if (null != lstTask)
            {
                using (var context = new TopmepEntities())
                {
                    //1.清除所有任務
                    string sql = "DELETE FROM PLAN_TASK WHERE PROJECT_ID=@projectid";
                    int i = context.Database.ExecuteSqlCommand(sql, new SqlParameter("projectid", project_id));
                    log.Debug("Remove Exist Task for projectid=" + project_id);
                    //2.匯入任務
                    foreach (PLAN_TASK pt in lstTask)
                    {
                        if (pt.TASK_NAME != null)
                        {
                            context.PLAN_TASK.Add(pt);
                        }else
                        {
                            log.Warn("task name is null:" + pt.PRJ_UID + ",id=" + pt.PRJ_ID);
                        }
                    }
                    i = context.SaveChanges();
                    log.Info("import task count=" + i);
                }
            }
        }
        private static EnumerableCollection ToEnumerable(Collection javaCollection)
        {
            return new EnumerableCollection(javaCollection);
        }
    }

    public class EnumerableCollection
    {
        public EnumerableCollection(Collection collection)
        {
            m_collection = collection;
        }

        public IEnumerator GetEnumerator()
        {
            return new Enumerator(m_collection);
        }

        private Collection m_collection;
    }

    public class Enumerator : IEnumerator
    {
        public Enumerator(Collection collection)
        {
            m_collection = collection;
            m_iterator = m_collection.iterator();
        }

        public object Current
        {
            get
            {
                return m_iterator.next();
            }
        }

        public bool MoveNext()
        {
            return m_iterator.hasNext();
        }

        public void Reset()
        {
            m_iterator = m_collection.iterator();
        }

        private Collection m_collection;
        private Iterator m_iterator;
    }
}