using Microsoft.AspNetCore.Mvc;

namespace VanDerHeijden.Logging.Web.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
	public IActionResult Index()
	{
		try
		{
			throw new Exception("Custom exception");
		}
		catch(Exception exception)
		{
			logger.LogError(exception, "Line1");
		}
		return View();
	}
}
