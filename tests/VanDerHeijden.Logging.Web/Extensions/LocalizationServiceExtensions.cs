using Microsoft.AspNetCore.Localization;
using System.Globalization;

namespace VanDerHeijden.Logging.Web.Extensions;

public static class LocalizationServiceExtensions
{
	public static IServiceCollection AddRequestLocalization(this IServiceCollection services)
	{
		services.Configure<RequestLocalizationOptions>(options =>
		{
			options.DefaultRequestCulture = new RequestCulture(CultureInfo.InvariantCulture);
			options.SupportedCultures = [CultureInfo.InvariantCulture];
			options.SupportedUICultures = [CultureInfo.InvariantCulture];
		});

		return services;
	}
}
