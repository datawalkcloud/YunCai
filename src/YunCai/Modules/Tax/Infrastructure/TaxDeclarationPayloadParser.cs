using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YunCai.Modules.Tax.Application;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 解析税局「申报信息查询」（DescribeSbmxcx2）的响应：Response.Data.List 里每条是一张已申报的申报表。
    /// 字段固定为税局自己的命名（yzpzzlMc / skssqq / skssqz…），这里只做日期归一、状态判别与所属期过滤。
    ///
    /// 该接口挂在电子税务局的「数字账户」子系统下，浏览器没进过它的页面时网关会另发一种
    /// {"code":"9999","msg":"会话失效，请重新登录！"} 的信封（HTTP 200、也是合法 JSON）。
    /// 这里把两种信封都认下来，避免把税局的原话吞成一句「没有返回申报明细」。
    /// </summary>
    internal static class TaxDeclarationPayloadParser
    {
        /// <summary>数字账户子系统会话未建立时，税局网关回的业务码。</summary>
        private const string AppSessionExpiredCode = "9999";

        /// <summary>响应是否是「数字账户会话未建立」的网关回执（调用方据此去补一次会话再重试）。</summary>
        public static bool NeedsAppSession(string body)
        {
            if (!TryReadJson(body, out var root) || root is not JsonObject obj || obj["Response"] is not null)
            {
                return false;
            }

            return GatewayCode(obj) == AppSessionExpiredCode;
        }

        /// <summary>解析申报查询响应，只保留所属期与所选月份有交集的行（税种申报页按所属期看）。</summary>
        public static TaxDeclarationsResultDto Parse(string body, DateOnly monthStart, DateOnly monthEnd)
        {
            var reading = Read(body);
            if (reading.Failure is not null)
            {
                return reading.Failure;
            }

            var rows = reading.Rows.Where(row => OverlapsMonth(row.PeriodStart, row.PeriodEnd, monthStart, monthEnd)).ToList();
            if (rows.Count > 0)
            {
                return Success(rows, reading.Notes);
            }

            reading.Notes.Add($"回传 {reading.Items} 条明细，与所选所属期无交集或字段不完整：{Clip(body, 300)}");
            return Fail(reading.Notes, "所选税款所属期没有申报记录。");
        }

        /// <summary>
        /// 合并「报表查询」按所属期分段取回的若干份报文，只保留申报日期落在区间（含首尾）内的行。
        /// 税局限制单次查询的所属期跨度不超过一年，而上年度的年报、残保金可能今年才申报，
        /// 所以调用方按自然年分段取数、这里统一去重与排序；任一段被税局拒绝就整次查询失败，
        /// 半份申报表名单比明确报错更容易让会计漏报。
        /// </summary>
        public static TaxDeclarationsResultDto ParseReports(IReadOnlyList<string> bodies, DateOnly declareFrom, DateOnly declareTo)
        {
            var rows = new List<TaxDeclarationRowDto>();
            var notes = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var body in bodies)
            {
                var reading = Read(body);
                if (reading.Failure is not null)
                {
                    return reading.Failure;
                }

                foreach (var row in reading.Rows.Where(row => seen.Add(row.Key)))
                {
                    rows.Add(row);
                }

                notes.AddRange(reading.Notes);
            }

            var kept = rows.Where(row => WithinDeclareDate(row.DeclareDate, declareFrom, declareTo)).ToList();
            if (kept.Count > 0)
            {
                // 分段各自有序，合并后要按申报日期重新倒序，页面拿到的第一行才是最近一次申报。
                kept.Sort((left, right) => string.CompareOrdinal(right.DeclareDate, left.DeclareDate));
                return Success(kept, notes);
            }

            notes.Add(
                $"分段共回传 {rows.Count} 张申报表，申报日期均不在 {declareFrom:yyyy-MM-dd} 至 {declareTo:yyyy-MM-dd} 内。");
            return Fail(notes, "所选申报日期区间没有申报表。");
        }

        /// <summary>一次报文的解析产物：全部申报表行、税局回传的明细条数、诊断，以及取不到数据时的失败结果。</summary>
        private readonly record struct Reading(
            List<TaxDeclarationRowDto> Rows,
            int Items,
            List<string> Notes,
            TaxDeclarationsResultDto? Failure);

        /// <summary>
        /// 读报文并做字段级解析，不做任何区间过滤。取不到数据时 Failure 一定带上 Diagnostics
        /// （税局的原话摘要），便于前端展示与服务器日志复盘，不再只剩一句没有信息量的「没有返回申报明细」。
        /// </summary>
        private static Reading Read(string body)
        {
            var notes = new List<string>();
            var rows = new List<TaxDeclarationRowDto>();

            if (!TryReadJson(body, out var root))
            {
                notes.Add($"响应不是 JSON：{Clip(body, 300)}");
                return new Reading(rows, 0, notes, Fail(notes, "税局返回的不是申报数据，请稍后重试。"));
            }

            var response = root?["Response"];
            var error = response?["Error"];
            if (error is not null)
            {
                notes.Add($"税局报错：{Clip(body, 300)}");
                var text = error is JsonObject errorObject ? Text(errorObject, "Message") : "";
                return new Reading(rows, 0, notes, Fail(notes, string.IsNullOrWhiteSpace(text) ? "税局拒绝了本次申报查询。" : text));
            }

            if (response?["Data"] is not JsonObject data)
            {
                // 没有 Response.Data 就只剩两种可能：网关自己的 code/msg 回执，或接口换了信封。
                var gateway = DescribeGateway(root);
                notes.Add($"响应里没有 Response.Data：{Clip(body, 300)}");
                return new Reading(rows, 0, notes, Fail(notes, gateway ?? "税局没有返回申报明细，请稍后重试。"));
            }

            var items = ListItems(data);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in items)
            {
                var row = ParseRow(item);
                if (row is not null && seen.Add(row.Key))
                {
                    rows.Add(row);
                }
            }

            var total = Integer(data, "Total");
            if (total > items.Count)
            {
                notes.Add($"税局共有 {total} 条，本次只回传 {items.Count} 条，可能有申报记录没取到。");
            }

            rows.Sort((left, right) => string.CompareOrdinal(right.DeclareDate, left.DeclareDate));
            return new Reading(rows, items.Count, notes, null);
        }

        private static TaxDeclarationsResultDto Success(List<TaxDeclarationRowDto> rows, List<string> notes)
        {
            return new TaxDeclarationsResultDto
            {
                Rows = rows,
                Diagnostics = notes.Count > 0 ? notes : null
            };
        }

        /// <summary>取不到数据时的统一出口：把诊断随结果一起带回，免得前端只剩一句没有信息量的提示。</summary>
        private static TaxDeclarationsResultDto Fail(List<string> notes, string message)
        {
            return new TaxDeclarationsResultDto
            {
                Message = message,
                Diagnostics = notes
            };
        }

        private static bool TryReadJson(string body, out JsonNode? root)
        {
            root = null;
            if (string.IsNullOrWhiteSpace(body))
            {
                return false;
            }

            try
            {
                root = JsonNode.Parse(body);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>网关 code/msg 信封里的原话；不是这种信封时返回 null。</summary>
        private static string? DescribeGateway(JsonNode? root)
        {
            if (root is not JsonObject obj)
            {
                return null;
            }

            var message = Text(obj, "msg");
            if (message.Length == 0)
            {
                message = Text(obj, "message");
            }

            if (message.Length == 0)
            {
                return null;
            }

            return GatewayCode(obj) is { Length: > 0 } code ? $"税局提示：{message}（{code}）" : $"税局提示：{message}";
        }

        private static string GatewayCode(JsonObject obj)
        {
            if (obj["code"] is not JsonValue value)
            {
                return "";
            }

            return value.TryGetValue<string>(out var text) ? text.Trim() : value.ToString().Trim();
        }

        /// <summary>
        /// 取 Response.Data.List。税局在只有一张申报表时会把它序列化成对象而不是数组，
        /// 这里一并收拢成对象列表，否则整批记录会被当成「没有申报明细」。
        /// </summary>
        private static List<JsonObject> ListItems(JsonObject data)
        {
            return data["List"] switch
            {
                JsonArray array => array.OfType<JsonObject>().ToList(),
                JsonObject single => [single],
                _ => []
            };
        }

        /// <summary>读一个整数计数字段；税局有时把它写成字符串。</summary>
        private static int Integer(JsonObject obj, string key)
        {
            if (obj[key] is not JsonValue value)
            {
                return 0;
            }

            if (value.TryGetValue<int>(out var number))
            {
                return number;
            }

            return int.TryParse(Text(obj, key), out var parsed) ? parsed : 0;
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

        /// <summary>申报日期是否落在区间内（含首尾）；税局没给申报日期的行无法定位区间，按不匹配处理。</summary>
        private static bool WithinDeclareDate(string declareDate, DateOnly from, DateOnly to)
        {
            return DateOnly.TryParseExact(declareDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date >= from
                && date <= to;
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
