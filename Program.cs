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
// Firebase Admin reads the service-account credentials from the
// GOOGLE_APPLICATION_CREDENTIALS environment variable.
//
// Example:
// GOOGLE_APPLICATION_CREDENTIALS=/secure/firebase/service-account.json
//
// Never put the service-account JSON file inside the Flutter app
// or commit it to Git.
if (FirebaseApp.DefaultInstance == null)
{
    FirebaseApp.Create(new AppOptions
    {
        Credential = GoogleCredential.GetApplicationDefault()
    });
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
// Set Jwt:Key in appsettings.json or environment variables.
// Do not use the fallback key in production.
var jwtKey =
    builder.Configuration["Jwt:Key"]
    ?? "SuperSecretKeyForQatarLearningPlatformWithEnoughLength2026!";

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
    var dbContext =
        scope.ServiceProvider.GetRequiredService<AppDbContext>();

    dbContext.Database.EnsureCreated();
}

app.Run();