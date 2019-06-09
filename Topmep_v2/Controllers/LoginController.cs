using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Topmep.Service;

namespace Topmep.Controllers
{
    public class LoginController : Controller
    {
        private static Logger log = NLog.LogManager.GetCurrentClassLogger();
        // GET: Login
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Login(FormCollection f)
        {
            log.Info(f["userid"] +" login by "+ f["password"]);
            UserService service = new UserService();
            if (service.Login(f["userid"], f["password"]) != null)
            {
                Session["LoginUser"] = service.loginUser;
                Session["HasFunctions"] = service.userPrivilege;
                return RedirectToAction("Index", "Home", null);
            }
            ViewBag.Message = "登入失敗，請聯繫系統管理員!!";
            return View("Index");
        }
    }
}