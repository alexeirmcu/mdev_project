using System.Text;
using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using OpenAI;
using SmartTripPlanner.API.Middleware;
using SmartTripPlanner.API.Services;
using SmartTripPlanner.ApplicationServices;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Domain.Ports;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.LLM;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .SelectMany(e => e.Value!.Errors)
                .Select(e => new SmartTripPlanner.Domain.ApiModels.ValidationResult(
                    SmartTripPlanner.Domain.ApiModels.ErrorCode.VALIDATION_ERROR,
                    e.ErrorMessage))
                .ToList();

            return new UnprocessableEntityObjectResult(errors);
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

builder.Services.AddHttpContextAccessor();

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("Jwt:Secret is not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is not configured");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IUserContext, HttpUserContext>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connectionString!);
builder.Services.AddApplicationServices(builder.Configuration);

builder.Services.AddSingleton<IChatClient>(sp =>
{
    var llmOptions = sp.GetRequiredService<IOptions<LlmApiOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("Configuring LLM client with BaseUrl: {BaseUrl}, Model: {Model}", 
        llmOptions.BaseUrl, llmOptions.Model);
    
    var client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(llmOptions.ApiKey),
        new OpenAIClientOptions { Endpoint = new Uri(llmOptions.BaseUrl) });
    return client.GetChatClient(llmOptions.Model).AsIChatClient();
});

builder.Services.AddTransient<ExceptionHandlingMiddleware>();

builder.Services.Configure<PlaceSearchOptions>(
    builder.Configuration.GetSection(PlaceSearchOptions.SectionName));

builder.Services.AddSingleton<IMapper>(sp =>
{
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var expression = new MapperConfigurationExpression();
    expression.AddMaps(typeof(Program).Assembly);
    var config = new MapperConfiguration(expression, loggerFactory);
    config.AssertConfigurationIsValid();
    return config.CreateMapper();
});

var app = builder.Build();

// Apply pending EF Core migrations on startup (skip in Test environment for integration tests).
if (!app.Environment.IsEnvironment("Test"))
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
        await db.Database.MigrateAsync();
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
