using Microsoft.AspNetCore.Mvc.Razor;

namespace VanDerHeijden.Logging.Web.Extensions;

public static class RazorServiceExtensions
{
	public static IServiceCollection AddWwwRootRazor(this IServiceCollection services)
	{
		services.AddRazorPages(o => o.RootDirectory = "/wwwroot");

		services.Configure<RazorViewEngineOptions>(options =>
		{
			options.ViewLocationFormats.Clear();
			options.ViewLocationFormats.Add("/wwwroot/{0}.cshtml");
		});

		return services;
	}
}