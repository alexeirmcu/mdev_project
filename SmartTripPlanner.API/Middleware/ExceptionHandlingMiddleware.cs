using System.Text.Json;
using System.Text.Json.Serialization;
using SmartTripPlanner.ApplicationServices.Behaviors;
using SmartTripPlanner.Domain.ApiModels;
using SmartTripPlanner.Domain.Base;

namespace SmartTripPlanner.API.Middleware;

internal sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);

            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var statusCode = GetStatusCode(exception);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        if (statusCode == StatusCodes.Status422UnprocessableEntity)
        {
            var errors = GetValidationErrors(exception);
            await context.Response.WriteAsync(Serialize(errors));
        }
        else
        {
            var response = new
            {
                errorId = Guid.NewGuid().ToString(),
                title = exception.Message,
                statusCode
            };

            _logger.LogError(exception,
                "Error {Method} {Path} - {ErrorId}: {Message}",
                context.Request.Method, context.Request.Path,
                response.errorId, response.title);

            await context.Response.WriteAsync(Serialize(response));
        }
    }

    private static int GetStatusCode(Exception exception) => exception switch
    {
        DomainException => StatusCodes.Status422UnprocessableEntity,
        _ => StatusCodes.Status500InternalServerError
    };

    private static List<ValidationResult> GetValidationErrors(Exception exception)
    {
        if (exception is ValidationRequestException validationEx)
            return validationEx.Errors.ToList();

        return new List<ValidationResult>
        {
            new(ErrorCode.VALIDATION_ERROR, exception.Message)
        };
    }

    private static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        });
    }
}
