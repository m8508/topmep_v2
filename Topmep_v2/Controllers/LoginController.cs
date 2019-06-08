using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

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
            return RedirectToAction("Index", "Home", null);
            //return View("Index");
        }
    }
}