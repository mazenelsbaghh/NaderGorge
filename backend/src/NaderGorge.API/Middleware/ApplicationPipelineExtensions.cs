namespace NaderGorge.API.Middleware;

public static class ApplicationPipelineExtensions
{
    public static IApplicationBuilder UseErrorAwareRequestPerformance(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestPerformanceLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
