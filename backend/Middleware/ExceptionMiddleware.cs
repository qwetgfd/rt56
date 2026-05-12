using System.Net;
using System.Text.Json;
using Sharepoint_Plugin.Constants;
using Sharepoint_Plugin.Models;

namespace Sharepoint_Plugin.Middleware;

public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (statusCode, message) = GetErrorResponse(ex);
            _logger.LogError(ex, MessageConstants.RequestFailed);
            await WriteErrorResponse(context, statusCode, message);
        }
    }

    private static (HttpStatusCode StatusCode, string Message) GetErrorResponse(Exception ex) => ex switch
    {
        ArgumentException => (HttpStatusCode.BadRequest, ex.Message),
        UnauthorizedAccessException => (HttpStatusCode.Unauthorized, ex.Message),
        HttpRequestException httpEx => (httpEx.StatusCode ?? HttpStatusCode.BadGateway, ex.Message),
        _ => (HttpStatusCode.InternalServerError, MessageConstants.UnexpectedError)
    };

    private static async Task WriteErrorResponse(HttpContext context, HttpStatusCode statusCode, string message)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";
        var response = JsonSerializer.Serialize(APIResponse.Fail(message, new { statusCode = (int)statusCode }));
        await context.Response.WriteAsync(response);
    }
}
