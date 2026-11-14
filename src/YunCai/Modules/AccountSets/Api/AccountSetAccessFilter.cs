using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using YunCai.Modules.AccountSets.Application;
using YunCai.Modules.AccountSets.Domain;
using YunCai.SharedKernel.Infrastructure.Security;

namespace YunCai.Modules.AccountSets.Api
{
    /// <summary>
    /// 标记「除成员身份外还须是账套管理员」的端点（成员 / 角色 / 高危操作）。
    /// 由 <see cref="AccountSetAccessFilter"/> 统一判定。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class RequireAccountSetAdminAttribute : Attribute
    {
    }

    /// <summary>
    /// 声明端点需要的权限键；多个键之间是「任一满足」。未标注时按控制器所属模块
    /// 与 HTTP 方法推断（GET→查看、写→编辑、导入 / 导出 / 打印按动作名识别）。
    /// 权限矩阵只在账套内有意义，账套管理员自动拥有全部权限。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequirePermissionAttribute : Attribute
    {
        public RequirePermissionAttribute(params string[] keys)
        {
            Keys = keys;
        }

        public IReadOnlyList<string> Keys { get; }
    }

    /// <summary>
    /// 全局账套访问校验：凡路由带账套标识的接口，只允许该账套的启用成员访问，
    /// 并按角色权限矩阵校验「模块.动作」。
    /// 业务控制器统一用 <c>accountsets/{accountSetId:guid}/...</c>；账套管理控制器用 <c>{id:guid}</c>。
    /// 账套不存在时放行，由各控制器按原有口径回 404 / 400，避免把「不存在」暴露成 403。
    /// </summary>
    public sealed class AccountSetAccessFilter : IAsyncActionFilter
    {
        /// <summary>控制器 → 权限模块。未列出的控制器（认证、系统用户管理）不做账套权限校验。</summary>
        private static readonly Dictionary<string, string> ControllerModules = new(StringComparer.OrdinalIgnoreCase)
        {
            ["VouchersController"] = "vouchers",
            ["VoucherTemplatesController"] = "vouchers",
            ["JournalsController"] = "funds",
            ["TransfersController"] = "funds",
            ["BillsController"] = "funds",
            ["FundReportsController"] = "funds",
            ["EntryCategoriesController"] = "funds",
            ["InvoicesController"] = "invoices",
            ["ExpensesController"] = "invoices",
            ["SalariesController"] = "salary",
            ["FixedAssetsController"] = "assets",
            ["TaxPortalController"] = "tax",
            ["TaxPortalBrowserSessionController"] = "tax",
            ["TaxLoginTicketController"] = "tax",
            ["ClosingController"] = "closing",
            ["LedgersController"] = "ledgers",
            ["ReportsController"] = "reports",
            ["CustomReportsController"] = "reports",
            ["AuditController"] = "reports",
            ["AccountsController"] = "accounts",
            ["AssistItemsController"] = "assist",
            ["ArchivesController"] = "archives",
            ["CurrenciesController"] = "settings",
            ["VoucherWordsController"] = "settings",
            ["MembersController"] = "settings",
            ["RolesController"] = "settings",
            ["OperationLogsController"] = "operationlog",
            ["BackupController"] = "backup",
            ["BudgetController"] = "budget",
        };

        private readonly IAccountSetRepository _accountSetRepository;
        private readonly IAccountSetMemberRepository _memberRepository;

        public AccountSetAccessFilter(
            IAccountSetRepository accountSetRepository,
            IAccountSetMemberRepository memberRepository)
        {
            _accountSetRepository = accountSetRepository;
            _memberRepository = memberRepository;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (context.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any()
                || !TryGetAccountSetId(context, out var accountSetId))
            {
                await next();
                return;
            }

            var cancellationToken = context.HttpContext.RequestAborted;
            if (!await _accountSetRepository.ExistsAsync(accountSetId, cancellationToken))
            {
                await next();
                return;
            }

            var user = context.HttpContext.User;
            var userId = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            var phone = user.FindFirstValue("phone");
            var access = await _memberRepository.GetAccessAsync(accountSetId, userId, phone, cancellationToken);
            if (access is null)
            {
                context.Result = Forbidden("无权访问该账套。");
                return;
            }

            if (context.ActionDescriptor.EndpointMetadata.OfType<RequireAccountSetAdminAttribute>().Any() && !access.IsAdmin)
            {
                context.Result = Forbidden("该操作需要账套管理员权限。");
                return;
            }

            var required = ResolveRequiredKeys(context);
            if (required.Count > 0 && !required.Any(key => access.Permissions.Contains(key)))
            {
                context.Result = Forbidden($"该操作需要权限：{string.Join(" 或 ", required.Select(Describe))}。");
                return;
            }

            if (context.ActionDescriptor.EndpointMetadata.OfType<RequireStepUpAttribute>().Any())
            {
                var stepUp = context.HttpContext.RequestServices.GetRequiredService<IStepUpValidator>();
                var token = context.HttpContext.Request.Headers["X-Step-Up-Token"].FirstOrDefault();
                if (!stepUp.Validate(token, userId))
                {
                    context.Result = new ObjectResult(new
                    {
                        message = "该操作需要二次身份确认，请重新输入登录密码或手机验证码。",
                        code = "step_up_required"
                    })
                    {
                        StatusCode = StatusCodes.Status428PreconditionRequired
                    };
                    return;
                }
            }

            await next();
        }

        /// <summary>端点声明的权限键；没有声明时按模块 + 动作推断。</summary>
        private static IReadOnlyList<string> ResolveRequiredKeys(ActionExecutingContext context)
        {
            var declared = context.ActionDescriptor.EndpointMetadata
                .OfType<RequirePermissionAttribute>()
                .SelectMany(a => a.Keys)
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (declared.Count > 0)
            {
                return declared;
            }

            var descriptor = context.ActionDescriptor as ControllerActionDescriptor;
            var controller = descriptor?.ControllerTypeInfo.Name ?? context.Controller.GetType().Name;

            // 账套管理控制器动作性质差别大，按动作名显式映射。
            if (controller == nameof(AccountSetsController))
            {
                return descriptor?.ActionName switch
                {
                    "Create" => ["accountset-add.execute"],
                    "Update" or "SetAssistTypes" or "CreateTaxLoginTicket" => ["settings.edit"],
                    "Delete" => ["accountset-delete.execute"],
                    "ClosePeriod" or "ReopenPeriod" => ["closing.edit"],
                    // 列表 / 详情 / 切换当前账套只要求成员身份。
                    _ => []
                };
            }

            if (controller == nameof(ReinitController))
            {
                return ["reinit.execute"];
            }

            if (!ControllerModules.TryGetValue(controller, out var module))
            {
                return [];
            }

            var action = ResolveActionKey(module, descriptor?.ActionName, descriptor?.AttributeRouteInfo?.Template,
                context.HttpContext.Request.Method);
            return action is null ? [] : [$"{module}.{action}"];
        }

        /// <summary>把 HTTP 方法 / 动作名映射到目录里的动作键，并回落到模块确实支持的动作。</summary>
        private static string? ResolveActionKey(string module, string? actionName, string? routeTemplate, string method)
        {
            var definition = RolePermissionCatalog.Modules.FirstOrDefault(m => m.Key == module);
            if (definition is null)
            {
                return null;
            }

            var available = definition.Actions.Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
            var name = (actionName ?? string.Empty).ToLowerInvariant();
            var route = (routeTemplate ?? string.Empty).ToLowerInvariant();
            var isGet = string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase);
            var isDelete = string.Equals(method, "DELETE", StringComparison.OrdinalIgnoreCase);

            string candidate;
            if (name.Contains("import") || route.Contains("import"))
            {
                candidate = "import";
            }
            else if (name.Contains("export") || route.Contains("export"))
            {
                candidate = "export";
            }
            else if (name.Contains("pdf") || name.Contains("print") || route.EndsWith("/pdf", StringComparison.Ordinal))
            {
                candidate = "print";
            }
            else if (isDelete || name.Contains("purge") || name.Contains("clear") || name.Contains("recycle"))
            {
                candidate = "delete";
            }
            else if (isGet)
            {
                candidate = "view";
            }
            else
            {
                candidate = "edit";
            }

            if (available.Contains(candidate))
            {
                return candidate;
            }

            // 模块没有这个动作（如工资没有导出键）时，读回落查看、写回落编辑。
            if (isGet)
            {
                return available.Contains("view") ? "view" : null;
            }

            return available.Contains("edit") ? "edit" : available.Contains("execute") ? "execute" : null;
        }

        private static string Describe(string key)
        {
            var parts = key.Split('.', 2);
            var module = RolePermissionCatalog.Modules.FirstOrDefault(m => m.Key == parts[0]);
            var action = module?.Actions.FirstOrDefault(a => a.Key == (parts.Length > 1 ? parts[1] : string.Empty));
            return module is null ? key : $"{module.Name}·{action?.Name ?? parts[1]}";
        }

        private static bool TryGetAccountSetId(ActionExecutingContext context, out Guid accountSetId)
        {
            accountSetId = Guid.Empty;
            var routeValues = context.RouteData.Values;

            // 业务控制器：accountsets/{accountSetId}/...
            if (routeValues.TryGetValue("accountSetId", out var raw) && Guid.TryParse(raw?.ToString(), out accountSetId))
            {
                return true;
            }

            // 账套管理控制器自身的 {id} 也是账套标识；其他控制器的 {id} 是业务主键，不能当账套用。
            return context.Controller is AccountSetsController
                   && routeValues.TryGetValue("id", out var rawId)
                   && Guid.TryParse(rawId?.ToString(), out accountSetId);
        }

        private static ObjectResult Forbidden(string message)
            => new(new { message }) { StatusCode = StatusCodes.Status403Forbidden };
    }
}
