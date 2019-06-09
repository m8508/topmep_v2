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
    public class ContextService
    {
        public TopmepEntities db;// = new TopmepEntities();
        //定義上傳檔案存放路徑
        public static string strUploadPath = ConfigurationManager.AppSettings["UploadFolder"];
        public static string strTemplatePath = ConfigurationManager.AppSettings["TemplateFolder"];
        public static string quotesFolder = "Quotes"; //廠商報價單路徑
        public static string projectMgrFolder = "Project"; //施工進度管理資料夾
        //Sample Code : It can get ADO.NET Dataset
        public DataSet ExecuteStoreQuery(string sql, CommandType commandType, Dictionary<string, Object> parameters)
        {
            var result = new DataSet();
            // creates a data access context (DbContext descendant)
            using (var context = new TopmepEntities())
            {
                // creates a Command 
                var cmd = context.Database.Connection.CreateCommand();
                cmd.CommandType = commandType;
                cmd.CommandText = sql;
                // adds all parameters
                foreach (var pr in parameters)
                {
                    var p = cmd.CreateParameter();
                    p.ParameterName = pr.Key;
                    p.Value = pr.Value;
                    cmd.Parameters.Add(p);
                }
                try
                {
                    // executes
                    context.Database.Connection.Open();
                    var reader = cmd.ExecuteReader();

                    // loop through all resultsets (considering that it's possible to have more than one)
                    do
                    {
                        // loads the DataTable (schema will be fetch automatically)
                        var tb = new DataTable();
                        tb.Load(reader);                   
                        result.Tables.Add(tb);

                    } while (!reader.IsClosed);
                }
                finally
                {
                    // closes the connection
                    context.Database.Connection.Close();
                }
            }
            // returns the DataSet
            return result;
        }
        public DataTable getDataTable(string sql, Dictionary<string, Object> parameters)
        {
            DataTable dt = null;
            using (var context = new TopmepEntities())
            {
                SqlDataAdapter adapter = new SqlDataAdapter(sql, context.Database.Connection.ConnectionString);
                foreach (var pr in parameters)
                {
                    SqlParameter p = new SqlParameter(pr.Key, pr.Value);
                    adapter.SelectCommand.Parameters.Add(p);
                }
                DataSet ds = new DataSet();
                adapter.Fill(ds);
                dt = ds.Tables[0];
            }       
            return dt;
        }
    }

}