using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace KuaiJi.SharedKernel.Infrastructure.Api;

/// <summary>
/// 兜底异常处理：控制器各自捕获的业务异常走原有 BadRequest 文案，
/// 逃逸出来的异常统一转成 Problem Details，避免把堆栈写进响应体。
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, detail) = Map(exception);

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "请求 {Method} {Path} 处理失败。", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("请求 {Method} {Path} 未通过校验：{Message}", httpContext.Request.Method, httpContext.Request.Path, exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails =
            {
                Status = statusCode,
                Title = statusCode >= 500 ? "服务器内部错误" : "请求无法处理",
                Detail = detail
            }
        });
    }

    private (int StatusCode, string Detail) Map(Exception exception) => exception switch
    {
        // 服务层用 ArgumentException 表达「业务规则不满足」，文案本身就是给用户看的。
        ArgumentException => (StatusCodes.Status400BadRequest, exception.Message),
        // InvalidOperationException 也可能是框架内部错误（如 EF 查询无法翻译），
        // 只有出自本项目命名空间的才算业务校验失败。
        InvalidOperationException when IsOwnCode(exception) => (StatusCodes.Status400BadRequest, exception.Message),
        _ => (StatusCodes.Status500InternalServerError,
              environment.IsDevelopment() ? exception.ToString() : "服务器处理请求时发生错误，请稍后重试或联系管理员。")
    };

    private static bool IsOwnCode(Exception exception)
        => exception.TargetSite?.DeclaringType?.Namespace?.StartsWith("KuaiJi", StringComparison.Ordinal) == true;
}
