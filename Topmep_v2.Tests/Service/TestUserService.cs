using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Topmep.Models;
using Topmep.Service;

namespace Topmep.Tests.Service
{
    [TestClass]
    public class TestUserService
    {
        [TestMethod]
        public void TestGetPrivilege()
        {
            UserService target = new UserService();
            target.GetPrivilege("admin", "111111");
            //string output = JsonConvert.SerializeObject(target.userPrivilege);
            Console.WriteLine(target.userPrivilege.Count);
            foreach (SYS_MODULE m in target.userPrivilege)
            {
         
                if (m.SubModule != null)
                {
                    Console.WriteLine(m.MODULE_NAME + "-" + m.SubModule.Count);
                    foreach (SYS_MODULE sm in m.SubModule)
                    {
                        foreach (SYS_FUNCTION f in sm.Functions)
                        {
                            Console.WriteLine(m.MODULE_NAME + "-" + sm.MODULE_NAME + "-" + f.FUNCTION_NAME);
                        }
                    }
                }
                else
                {
                    foreach (SYS_FUNCTION f in m.Functions)
                    {
                        Console.WriteLine(m.MODULE_NAME + "-"  + f.FUNCTION_NAME);
                    }
                }
            }
           
            Assert.IsNotNull(target.userPrivilege);
        }
    }
}
