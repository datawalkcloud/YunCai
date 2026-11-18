using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;

namespace YunCai.SharedKernel.Infrastructure.Binding
{
    /// <summary>
    /// 放宽的 DateOnly 绑定器。生成的前端客户端把 Date 序列化成 ISO 8601 带时间缀
    /// （如 2026-01-01T00:00:00.000Z），而 DateOnly 只认 yyyy-MM-dd，默认绑定会直接报 400；
    /// 这里统一取前 10 位按不变文化解析，查询接口无需各自处理日期格式。
    /// </summary>
    public sealed class LenientDateOnlyModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext);

            var value = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            var raw = value.FirstValue;
            if (string.IsNullOrWhiteSpace(raw))
            {
                // 未传值：可空参数保持 null，必填参数仍由框架的必填校验报错。
                return Task.CompletedTask;
            }

            var text = raw.Trim();
            var datePart = text.Length >= 10 ? text.Substring(0, 10) : text;
            if (DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                bindingContext.Result = ModelBindingResult.Success(parsed);
                return Task.CompletedTask;
            }

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var utc))
            {
                bindingContext.Result = ModelBindingResult.Success(DateOnly.FromDateTime(utc.ToUniversalTime()));
                return Task.CompletedTask;
            }

            bindingContext.ModelState.AddModelError(bindingContext.ModelName, $"日期格式不正确：{raw}。");
            return Task.CompletedTask;
        }
    }

    /// <summary>把 <see cref="LenientDateOnlyModelBinder"/> 挂到 DateOnly / DateOnly? 上。</summary>
    public sealed class LenientDateOnlyModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            var modelType = context.Metadata.UnderlyingOrModelType;
            return modelType == typeof(DateOnly) ? new LenientDateOnlyModelBinder() : null;
        }
    }
}
