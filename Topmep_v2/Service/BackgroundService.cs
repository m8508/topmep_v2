﻿using Hangfire;
using Hangfire.Common;
using Hangfire.Logging;
using Hangfire.States;
using Hangfire.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using Topmep.Models;
using Topmep.Service;
using Topmep.Schedule;
using NLog;

namespace Topmep.Schedule
{
    /// <summary>
    /// 排程作業，啟動方法!!
    /// </summary>
    public class BackgroundService
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        [AutomaticRetry(Attempts = 20)]
        public void SendMailSchedule()
        {
            log.Info("SendMailSchedule start !!" + DateTime.Now);
            List<SYS_MESSAGE> lst = getTask();
            EMailService mailservice = new EMailService();
            foreach (SYS_MESSAGE m in lst)
            {
                using (var context = new TopmepEntities())
                {
                    try
                    {
                        log.Info("send msg=" + m.MSG_ID);
                        if (mailservice.SendMailByGmail(m.FROM_ADDRESS, m.DISPLAY_NAME, m.MAIL_LIST, m.BCC_MAIL_LIST, m.SUBJECT, m.MSG_BODY, null))
                        {

                            SYS_MESSAGE doneM = context.SYS_MESSAGE.Find(m.MSG_ID);
                            doneM.STATUS = "DONE";
                            context.SaveChanges();
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.Message + ":" + ex.StackTrace);
                    }
                }
                return;
            }
        }

        public List<SYS_MESSAGE> getTask()
        {
            List<SYS_MESSAGE> lst = null;
            string sql = "SELECT * FROM SYS_MESSAGE WHERE SEND_TIME < getdate() AND STATUS IS NULL";
            using (var context = new TopmepEntities())
            {
                try
                {
                    log.Debug("sql=" + sql);
                    lst = context.SYS_MESSAGE.SqlQuery(sql).ToList();
                }
                catch (Exception e)
                {
                    log.Error(e.Message + "," + e.StackTrace);
                }
            }
            return lst;
        }

    }
}
//HangFire Task Failure Event Sample
public class LogFailureAttribute : JobFilterAttribute, IApplyStateFilter
{
    private static readonly ILog Logger = LogProvider.GetCurrentClassLogger();

    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        var failedState = context.NewState as FailedState;
        if (failedState != null)
        {
            Logger.ErrorException(String.Format("Background job #{0} was failed with an exception.", context.JobId), failedState.Exception);
        }
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
    }
}