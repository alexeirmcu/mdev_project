using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Builder;
using OpenAI;
using SmartTripPlanner.API.Middleware;
using SmartTripPlanner.ApplicationServices;
using SmartTripPlanner.ApplicationServices.Configurations;
using SmartTripPlanner.Infrastructure;
using SmartTripPlanner.Infrastructure.LLM;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

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

// Apply pending EF Core migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<PlannerDbContext>();
    await db.Database.MigrateAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
