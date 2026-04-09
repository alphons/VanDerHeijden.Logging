using VanDerHeijden.Logging;
using VanDerHeijden.Logging.MongoDb;
using VanDerHeijden.Logging.Web.Extensions;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
	ContentRootPath = AppContext.BaseDirectory
});

builder.Logging
	.AddCustomLogger()
	.AddMongoDbLogger();

builder.Services.AddRequestLocalization();

builder.Services.AddMongoDbDatabase(builder.Configuration);
builder.Services.AddMongoDbLogging(builder.Configuration);

builder.Services.AddControllersWithViews();

builder.Services.AddWwwRootRazor();

builder.Services.AddHttpContextAccessor();


var app = builder.Build();

app.UseDeveloperExceptionPage();
app.UseRequestLocalization();
app.UseRouting();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapDefaultControllerRoute();

app.MapRazorPages();


await app.RunAsync();

