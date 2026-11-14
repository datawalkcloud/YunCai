namespace YunCai.SharedKernel.Infrastructure.Security
{
    /// <summary>
    /// 高危操作的二次身份确认令牌校验。令牌由登录用户用密码或手机验证码换取，
    /// 短时有效且一次性使用；具体签发逻辑在 Identity 模块，过滤器只依赖本接口。
    /// </summary>
    public interface IStepUpValidator
    {
        bool Validate(string? token, string userId);
    }

    /// <summary>标记需要二次身份确认的端点（结账、反结账、删除凭证、恢复备份等）。</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireStepUpAttribute : Attribute
    {
    }
}
