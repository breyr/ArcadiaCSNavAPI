using System.Security.Claims;
using System.Text;
using ArcadiaCSNavAPI.Contracts;
using ArcadiaCSNavAPI.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Supabase;

DotNetEnv.Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

// Supabase.Client is stateful so can't use Singleton
builder.Services.AddScoped<Supabase.Client>(_ =>
{
    var supabaseUrl = Environment.GetEnvironmentVariable("SupabaseUrl");
    var supabaseKey = Environment.GetEnvironmentVariable("SupabaseKey");

    if (string.IsNullOrEmpty(supabaseUrl) || string.IsNullOrEmpty(supabaseKey))
    {
        throw new InvalidOperationException("Supabase config is missing");
    }

    return new Supabase.Client(
        supabaseUrl,
        supabaseKey,
        new SupabaseOptions
        {
            AutoRefreshToken = true,
            AutoConnectRealtime = true,
        }
    );
});

// Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = Environment.GetEnvironmentVariable("SupabaseIssuer"),
            ValidAudience = "authenticated",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("SupabaseJWTSecret")))
        };

        // logging for authentication events
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine("Authentication failed.");
                Console.WriteLine(context.Exception);
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("Token validated.");
                return Task.CompletedTask;
            },
        };
    });

// Authorization
builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        builder =>
        {
            builder.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        }
    );
});

var app = builder.Build();

app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(config => config.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1"));
}

app.MapPost("/tracks", async (CreateTrackRequest req, Supabase.Client client, HttpContext context, ILogger<Program> logger) =>
{
    // access JWT claims
    var claims = context.User.Claims;
    var userIdClaim = claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
    if (userIdClaim != null)
    {
        // create a new track record with the users's Id
        var trackRecord = new Track
        {
            UserId = userIdClaim.Value,
            TrackName = req.TrackName,
            CompletedId = req.CompletedId
        };
        var trackRes = await client.From<Track>().Insert(trackRecord);
        var newTrackRecord = trackRes.Models.First(); // just added track record, type Track
        return Results.Ok($"Added progress: (row id) {newTrackRecord.Id}");
    }
    // otherwise bad request
    logger.LogWarning("User ID claim not found in JWT.");
    return Results.BadRequest("User ID claim not found in JWT.");
}).RequireAuthorization();

app.UseHttpsRedirection();

app.Run();
