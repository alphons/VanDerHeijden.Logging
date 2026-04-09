using Microsoft.AspNetCore.Mvc;

namespace VanDerHeijden.Logging.Web.Controllers;

public class HomeController : Controller
{
	public IActionResult Index()
	{
		return View();
	}
}
