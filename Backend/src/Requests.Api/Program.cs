using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Requests.Api;
using Requests.Api.Auth;
using Requests.Api.ErrorHandling;
using Requests.Application;
using Requests.Infrastructure;
using Requests.Infrastructure.Auth;
using Requests.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Structured logging via Serilog, writing to the console.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {NewLine}{Exception}"));

// [ApiController] already returns an RFC 7807 ValidationProblemDetails (HTTP 400) on invalid
// model state, so no separate model-validation filter is needed.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Include XML docs from the API and Application assemblies so Swagger shows descriptions.
    foreach (var assembly in new[] { Assembly.GetExecutingAssembly(), typeof(Requests.Application.Requests.RequestSearchRequest).Assembly })
    {
        var xmlFile = $"{assembly.GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
        }
    }
});

// Compose DI per layer via each layer's Binder (platform convention). Infrastructure registers
// the DbContext, repositories, JWT settings, and the token issuer (JwtTokenService).
builder.Services.UseInfrastructure(builder.Configuration);
builder.Services.UseApplication();

// JWT bearer *validation* is a host concern: read the settings and validate incoming tokens.
var jwt = builder.Configuration.GetSection("Jwt").Get<JwtSettings>() ?? new JwtSettings();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddHealthChecks();

// Allow the browser-based Angular client (separate origin) to call the API.
// The origin is read from configuration rather than hardcoded.
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy => policy
    .WithOrigins(builder.Configuration["Cors:FrontendOrigin"]!)
    .WithMethods("GET", "POST")
    .WithHeaders("Content-Type", "Authorization")));

// Global last-resort handler: unhandled exceptions -> HTTP 500 ProblemDetails (no internals leaked).
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseSerilogRequestLogging();

// Positioned early so it wraps the rest of the pipeline and catches any unhandled exception.
app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RequestsDbContext>();
    DbSeeder.Seed(db);
    // Seed a default administrator so there is always an admin account to sign in with
    // (credentials come from configuration; the password is stored only as a hash).
    UserSeeder.SeedAdmin(db, builder.Configuration);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

app.MapControllers();

app.Run();

public partial class Program { }
