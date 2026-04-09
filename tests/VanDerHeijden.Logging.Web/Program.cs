using Microsoft.AspNetCore.Mvc.Razor;
using System.Globalization;
using VanDerHeijden.Logging;
using VanDerHeijden.Logging.MongoDb;
using VanDerHeijden.Logging.Web.Extensions;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
	ContentRootPath = AppContext.BaseDirectory
});
var services = builder.Services;

builder.Logging
	.AddCustomLogger()
	.AddMongoDbLogger();

services.AddMongoDbDatabase(builder.Configuration);
services.AddMongoDbLogging(builder.Configuration);

services.AddControllersWithViews();

services.AddRazorPages(o => o.RootDirectory = "/wwwroot");

services.Configure<RazorViewEngineOptions>(options =>
{
	options.ViewLocationFormats.Add("/wwwroot/{0}.cshtml");
});

services.AddHttpContextAccessor();

services.AddMvcCore();
//services.AddJsonBodyProvider();

var app = builder.Build();

app.UseRouting();

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseDeveloperExceptionPage();

app.MapControllers();
app.MapDefaultControllerRoute();

app.MapRazorPages();

await app.RunAsync();

