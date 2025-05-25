using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Reflection;
using MyCalendar.Application;
using MyCalendar.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "MyCalendar API",
        Version = "v1",
        Description = @"
## Scheduling System with Conflict Detection

### How to use:
1. **Create users** - POST /api/Users
2. **Schedule meeting** - POST /api/Meetings/schedule
3. **View conflicts** - API returns 409 + suggestions automatically
4. **Find slots** - GET /api/Meetings/available-slots

### Example workflow:
- Create 2-3 users
- Try to schedule meetings at the same time
- See alternative time suggestions!

### Demo Interface: [/demo](/demo)",
        Contact = new OpenApiContact
        {
            Name = "Lucas Duarte",
            Email = "contato@dvarte.dev"
        }
    });

    c.EnableAnnotations();
    c.UseInlineDefinitionsForEnums();
    
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyCalendar API v1");
        c.RoutePrefix = string.Empty;
        c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
        c.DefaultModelsExpandDepth(2);
        c.DisplayRequestDuration();
        c.EnableDeepLinking();
        c.EnableFilter();
        c.ShowExtensions();
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();

app.UseStaticFiles();

app.MapGet("/demo", async context =>
{
    var htmlPath = Path.Combine(app.Environment.WebRootPath, "demo.html");
    if (File.Exists(htmlPath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(htmlPath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Demo not found");
    }
});

app.MapGet("/endpoints", () => new
{
    message = "MyCalendar API Endpoints",
    swagger = "/",
    demo = "/demo",
    api = "/api",
    endpoints = new[]
    {
        "GET  /api/Users - List users",
        "POST /api/Users - Create user", 
        "POST /api/Meetings/schedule - Schedule meeting",
        "GET  /api/Meetings/available-slots - Find available slots",
        "GET  /api/Test - Test API",
        "GET  /api/Test/db-connection - Test DB"
    }
});

app.MapControllers();

app.Run();

// Make Program class accessible for integration tests
public partial class Program { }