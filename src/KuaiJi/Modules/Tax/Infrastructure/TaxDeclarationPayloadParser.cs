using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiJi.Modules.Tax.Application;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 解析税局「申报信息查询」（DescribeSbmxcx2）的响应：Response.Data.List 里每条是一张已申报的申报表。
    /// 字段固定为税局自己的命名（yzpzzlMc / skssqq / skssqz…），这里只做日期归一、状态判别与所属期过滤。
    /// </summary>
    internal static class TaxDeclarationPayloadParser
    {
        public static TaxDeclarationsResultDto Parse(
            string body,
            DateOnly monthStart,
            DateOnly monthEnd,
            out IReadOnlyList<string> diagnostics)
        {
            diagnostics = Array.Empty<string>();

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                diagnostics = [Clip(body, 300)];
                return new TaxDeclarationsResultDto
                {
                    Message = "税局返回的不是申报数据，请稍后重试。"
                };
            }

            var response = root?["Response"];
            var error = response?["Error"];
            if (error is not null)
            {
                return new TaxDeclarationsResultDto
                {
                    Message = error["Message"]?.GetValue<string>() ?? "税局拒绝了本次申报查询。"
                };
            }

            if (response?["Data"]?["List"] is not JsonArray list)
            {
                diagnostics = [Clip(body, 300)];
                return new TaxDeclarationsResultDto
                {
                    Message = "税局没有返回申报明细，请稍后重试。"
                };
            }

            var rows = new List<TaxDeclarationRowDto>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in list)
            {
                if (node is not JsonObject item)
                {
                    continue;
                }

                var row = ParseRow(item);
                if (row is null)
                {
                    continue;
                }

                // 查询按「所属期起」落在季度内过滤，季报会整段跨月；页面按所选月份再筛一次交集。
                if (!OverlapsMonth(row.PeriodStart, row.PeriodEnd, monthStart, monthEnd))
                {
                    continue;
                }

                if (seen.Add(row.Key))
                {
                    rows.Add(row);
                }
            }

            rows.Sort((left, right) => string.CompareOrdinal(right.DeclareDate, left.DeclareDate));
            return new TaxDeclarationsResultDto
            {
                Rows = rows,
                Message = rows.Count > 0 ? null : "所选税款所属期没有申报记录。"
            };
        }

        private static TaxDeclarationRowDto? ParseRow(JsonObject item)
        {
            var kind = CleanKind(Text(item, "yzpzzlMc"));
            if (kind.Length == 0)
            {
                return null;
            }

            var periodStart = Date(item, "skssqq");
            var periodEnd = Date(item, "skssqz");
            if (periodStart.Length == 0 || periodEnd.Length == 0)
            {
                return null;
            }

            var declareDate = Date(item, "nssbrq");
            var voidDate = Date(item, "zfrq");
            var voided = string.Equals(Text(item, "zfbz"), "Y", StringComparison.OrdinalIgnoreCase)
                || voidDate.Length > 0;
            // gzlx 是「更正类型」：正常申报为「新产生申报表」，更正过的会带「更正」字样。
            var correction = Text(item, "gzlx");
            var corrected = !voided && correction.Contains("更正", StringComparison.Ordinal);
            var payable = Amount(item, "ybtse");

            var status = voided ? "已作废" : corrected ? "已更正" : "已申报";
            var feedback = voided
                ? (voidDate.Length > 0 ? $"作废日期 {voidDate}" : "申报表已作废")
                : corrected ? correction : "";

            var id = Text(item, "sbuuid");
            if (id.Length == 0)
            {
                id = Text(item, "pzxh");
            }

            return new TaxDeclarationRowDto
            {
                Key = id.Length > 0
                    ? id
                    : $"{kind}|{periodStart}|{periodEnd}|{declareDate}|{payable.ToString(CultureInfo.InvariantCulture)}",
                DeclareKind = kind,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Status = status,
                Payable = payable,
                DeclareDate = declareDate,
                Feedback = feedback
            };
        }

        private static bool OverlapsMonth(string start, string end, DateOnly monthStart, DateOnly monthEnd)
        {
            if (!DateOnly.TryParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from)
                || !DateOnly.TryParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var to))
            {
                return true;
            }

            return from <= monthEnd && to >= monthStart;
        }

        private static string CleanKind(string raw)
        {
            var text = raw.Trim().Trim('《', '》', ' ').Trim();
            return text;
        }

        private static string Text(JsonObject obj, string key)
        {
            return obj[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text.Trim() : "";
        }

        private static string Date(JsonObject obj, string key)
        {
            var text = Text(obj, key);
            if (text.Length >= 10
                && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                return date.ToString("yyyy-MM-dd");
            }

            return "";
        }

        private static decimal Amount(JsonObject obj, string key)
        {
            if (obj[key] is JsonValue value)
            {
                if (value.TryGetValue<decimal>(out var amount))
                {
                    return amount;
                }

                if (value.TryGetValue<double>(out var number))
                {
                    return (decimal)number;
                }
            }

            return 0m;
        }

        private static string Clip(string value, int max)
        {
            return value.Length <= max ? value : value[..max] + "…";
        }
    }
}
