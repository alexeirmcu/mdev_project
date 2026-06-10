using AutoMapper;
using AutoMapper.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using SmartTripPlanner.ApplicationServices;
using SmartTripPlanner.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddInfrastructure(connectionString!);
builder.Services.AddApplicationServices(builder.Configuration);

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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
