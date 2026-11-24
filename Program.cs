using System.Text;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QatarLearningBackend.Data;
using QatarLearningBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------------------------------------
// Firebase Admin SDK
// ------------------------------------------------------------
// Credential lookup order:
//   1. Firebase:CredentialsPath (appsettings / environment variable)
//   2. <app folder>/secrets/qatar-firebase-admin.json
//   3. GOOGLE_APPLICATION_CREDENTIALS environment variable
// If none work, the site still starts and push notifications are
// disabled (a message is written to the console / stdout log).
//
// Never commit the service-account JSON to Git.
if (FirebaseApp.DefaultInstance == null)
{
    try
    {
        var credPath = builder.Configuration["Firebase:CredentialsPath"];

        if (string.IsNullOrWhiteSpace(credPath))
        {
            credPath = Path.Combine(
                builder.Environment.ContentRootPath,
                "secrets",
                "qatar-firebase-admin.json");
        }

        var credential = File.Exists(credPath)
            ? GoogleCredential.FromFile(credPath)
            : GoogleCredential.GetApplicationDefault();

        FirebaseApp.Create(new AppOptions
        {
            Credential = credential
        });

        Console.WriteLine("Firebase initialized.");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"Firebase init failed, push notifications disabled: {ex.Message}");
    }
}

// ------------------------------------------------------------
// Services
// ------------------------------------------------------------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ------------------------------------------------------------
// Swagger with JWT "Authorize" button
// ------------------------------------------------------------
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QatarLearningBackend",
        Version = "1.0"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description =
            "Paste ONLY the JWT token. Swagger adds the 'Bearer ' prefix."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ------------------------------------------------------------
// MySQL / Entity Framework Core
// ------------------------------------------------------------
var connectionString =
    builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:DefaultConnection is not configured.");
}

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(
        connectionString,
        ServerVersion.AutoDetect(connectionString)));

// ------------------------------------------------------------
// Application services
// ------------------------------------------------------------

// Creates database notification records for booking events.
builder.Services.AddScoped<
    INotificationService,
    NotificationService>();

// Sends push notifications through Firebase Cloud Messaging.
builder.Services.AddScoped<
    IFcmPushService,
    FcmPushService>();

// ------------------------------------------------------------
// JWT Authentication
// ------------------------------------------------------------
// Jwt:Key MUST be set in appsettings.json or environment variables.
// There is intentionally no fallback key.
var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured.");

var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme =
        JwtBearerDefaults.AuthenticationScheme;

    options.DefaultChallengeScheme =
        JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,

        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],

        IssuerSigningKey =
            new SymmetricSecurityKey(key),

        ClockSkew = TimeSpan.FromMinutes(1)
    };
});

builder.Services.AddAuthorization();

// ------------------------------------------------------------
// CORS
// ------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// ------------------------------------------------------------
// Build application
// ------------------------------------------------------------
var app = builder.Build();

// ------------------------------------------------------------
// HTTP request pipeline
// ------------------------------------------------------------
app.UseSwagger();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint(
        "/swagger/v1/swagger.json",
        "Qatar Learning Platform API V1");

    options.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet(
    "/",
    () =>
        "Qatar Teachers & Student Learning Platform API is running successfully!");

// ------------------------------------------------------------
// Ensure database exists on startup
// ------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext =
            scope.ServiceProvider.GetRequiredService<AppDbContext>();

        dbContext.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Database startup check failed: {ex.Message}");
        throw;
    }
}

app.Run();