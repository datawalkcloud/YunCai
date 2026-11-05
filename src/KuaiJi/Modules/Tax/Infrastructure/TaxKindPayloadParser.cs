using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KuaiJi.Modules.Tax.Application;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把税局「税费种认定」页面背后的 JSON 响应解析成行。
    ///
    /// 各省电子税务局的接口字段名并不统一（有的用拼音缩写 zsxm/sbzl，有的用中文键），
    /// 所以这里按「字段名语义 + 取值特征」做启发式映射，而不是绑定某省的固定 DTO。
    /// 解析不到时把候选数组的字段名回传，便于按真实响应补充规则。
    /// 除认定行外还顺带找页头的「征收类型」——它按户认定、不在表格列里，所以整棵响应都要扫。
    /// </summary>
    /// <summary>税费种报文的解析结果：认定行、页头的征收类型，以及没解析出行时的抓包摘要。</summary>
    internal sealed record TaxKindParseResult(
        IReadOnlyList<TaxKindRowDto> Rows,
        string? LevyType,
        IReadOnlyList<string> Diagnostics);

    internal static class TaxKindPayloadParser
    {
        private const int MaxRows = 500;
        private const int MaxItemsPerRow = 200;

        private static readonly string[] ProjectHints =
            ["zsxm", "zsxmmc", "zsmc", "sfxm", "sfxmmc", "taxkind", "taxitem", "taxtype", "征收项目", "税种", "税费种"];

        private static readonly string[] ItemNameHints =
            ["zspm", "zspmmc", "zszm", "征收品目", "品目名称", "征收子目", "子目名称"];

        private static readonly string[] DeclareKindHints =
            ["sbzl", "sblx", "sbzlmc", "declare", "declaration", "申报种类", "申报表", "申报项目"];

        private static readonly string[] PeriodHints =
            ["nsqx", "sbzq", "申报周期", "征收周期", "纳税期限", "sbqx", "申报期限", "zq", "period"];

        private static readonly string[] OptionHints =
            ["sbxx", "sbxz", "declareoption", "申报选项", "申报方式", "征收方式"];

        private static readonly string[] ValidFromHints =
            ["yxqq", "yxqks", "yxqstart", "startdate", "qssj", "ksrq", "有效期起", "起始日期", "开始日期", "认定日期"];

        private static readonly string[] ValidToHints =
            ["yxqz", "yxqjs", "yxqend", "enddate", "zzsj", "jsrq", "有效期止", "终止日期", "结束日期"];

        private static readonly string[] RateHints = ["sl", "rate", "shuilv", "税率", "征收率"];

        /// <summary>页头「征收类型」的字段名，按户认定，不属于认定行的某一列。</summary>
        private static readonly string[] LevyTypeHints =
            ["zslx", "zslxmc", "zslb", "qslx", "levytype", "collectiontype", "征收类型", "征收类别", "认定类型"];

        /// <summary>没有字段名可依据时，只认这几个税局实际会用到的取值，避免把别的「××型」字段当成征收类型。</summary>
        private static readonly string[] LevyTypeValues =
            ["服务型", "货物型", "工业型", "商贸型", "建筑型", "运输型", "劳务型"];

        public static TaxKindParseResult Parse(IReadOnlyList<CapturedJsonPayload> payloads)
        {
            var rows = new List<TaxKindRowDto>();
            var notes = new List<string>();
            string? levyType = null;

            foreach (var payload in payloads)
            {
                JsonNode? root;
                try
                {
                    root = JsonNode.Parse(payload.Body);
                }
                catch (JsonException)
                {
                    continue;
                }

                if (root is null)
                {
                    continue;
                }

                levyType ??= FindLevyType(root, 0);

                var arrays = new List<JsonArray>();
                CollectArrays(root, arrays, 0);
                var sample = "";
                foreach (var array in arrays)
                {
                    var parsed = ParseArray(array);
                    if (parsed.Count > 0)
                    {
                        rows.AddRange(parsed);
                    }
                    else if (sample.Length == 0 && array.Count > 0 && array[0] is JsonObject first)
                    {
                        sample = string.Join("|", first.Select(pair => pair.Key).Take(12));
                    }
                }

                if (sample.Length > 0)
                {
                    notes.Add($"{Shorten(payload.Url)} 字段={sample}");
                }
            }

            var deduped = Dedup(rows);
            IReadOnlyList<string> diagnostics = deduped.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return new TaxKindParseResult(deduped, levyType, diagnostics);
        }

        /// <summary>
        /// 找页头展示的征收类型。税局有时把它放在认定数组外的另一个对象里，所以整棵响应都要走一遍；
        /// 只回代码（如「1」）时不猜文字，直接当没取到。
        /// </summary>
        private static string? FindLevyType(JsonNode node, int depth)
        {
            if (depth > 8)
            {
                return null;
            }

            if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item is not null && FindLevyType(item, depth + 1) is { } hit)
                    {
                        return hit;
                    }
                }

                return null;
            }

            if (node is not JsonObject obj)
            {
                return null;
            }

            foreach (var pair in obj)
            {
                var key = NormalizeKey(pair.Key);
                if (!LevyTypeHints.Any(key.Contains) || !IsLevyType(TextOf(pair.Value), requireLabel: true))
                {
                    continue;
                }

                return TextOf(pair.Value)!.Trim();
            }

            foreach (var pair in obj)
            {
                if (IsLevyType(TextOf(pair.Value), requireLabel: false))
                {
                    return TextOf(pair.Value)!.Trim();
                }
            }

            foreach (var pair in obj)
            {
                if (pair.Value is not null && FindLevyType(pair.Value, depth + 1) is { } hit)
                {
                    return hit;
                }
            }

            return null;
        }

        /// <summary>
        /// 是不是一个能直接展示的征收类型。带「征收类型」字段名时放宽到任何「××型」，
        /// 只按取值找时必须命中已知词表。
        /// </summary>
        private static bool IsLevyType(string? raw, bool requireLabel)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            var text = raw.Trim();
            if (text.Length is < 2 or > 10 || !text.EndsWith('型'))
            {
                return false;
            }

            return requireLabel || LevyTypeValues.Contains(text, StringComparer.Ordinal);
        }

        private static void CollectArrays(JsonNode node, List<JsonArray> arrays, int depth)
        {
            if (depth > 8)
            {
                return;
            }

            if (node is JsonArray array)
            {
                if (array.Count > 0 && array.Any(item => item is JsonObject))
                {
                    arrays.Add(array);
                }

                foreach (var item in array)
                {
                    if (item is not null)
                    {
                        CollectArrays(item, arrays, depth + 1);
                    }
                }

                return;
            }

            if (node is JsonObject obj)
            {
                foreach (var pair in obj)
                {
                    if (pair.Value is not null)
                    {
                        CollectArrays(pair.Value, arrays, depth + 1);
                    }
                }
            }
        }

        private static List<TaxKindRowDto> ParseArray(JsonArray array)
        {
            var rows = new List<TaxKindRowDto>();
            foreach (var node in array)
            {
                if (node is not JsonObject obj)
                {
                    continue;
                }

                var row = ParseObject(obj);
                if (row is not null)
                {
                    rows.Add(row);
                    if (rows.Count >= MaxRows)
                    {
                        break;
                    }
                }
            }

            return rows;
        }

        private static TaxKindRowDto? ParseObject(JsonObject obj)
        {
            var project = FieldText(obj, ProjectHints, ItemNameHints)
                ?? FindByValue(obj, value => value.Length <= 20 && (value.EndsWith('税') || value.EndsWith('费')) && !value.Contains("申报"));
            // 只剩代码的行是税局的代码字典或「待办事项」网格，不是认定信息。
            if (string.IsNullOrWhiteSpace(project) || IsCodeValue(project))
            {
                return null;
            }

            var items = ParseItems(obj);
            var declareKind = FieldText(obj, DeclareKindHints, ItemNameHints) ?? "";
            var period = NormalizePeriod(FieldText(obj, PeriodHints, ItemNameHints));
            var option = FieldText(obj, OptionHints, ItemNameHints) ?? "";
            var validFrom = NormalizeDate(FieldText(obj, ValidFromHints, ItemNameHints) ?? "");
            var validTo = NormalizeDate(FieldText(obj, ValidToHints, ItemNameHints) ?? "");

            // 只有「征收项目」一个字段的数组多半是字典或下拉项，不当作税费种认定。
            if (declareKind.Length == 0 && period.Length == 0 && validFrom.Length == 0 && items.Count == 0)
            {
                return null;
            }

            return new TaxKindRowDto
            {
                Project = project,
                DeclareKind = declareKind,
                Period = period,
                Option = option,
                ValidFrom = validFrom,
                ValidTo = validTo,
                Items = items.Count > 0 ? items : null
            };
        }

        private static List<TaxKindItemDto> ParseItems(JsonObject obj)
        {
            var items = new List<TaxKindItemDto>();

            foreach (var pair in obj)
            {
                if (pair.Value is not JsonArray nested || nested.Count == 0)
                {
                    continue;
                }

                foreach (var node in nested)
                {
                    if (node is not JsonObject itemObj)
                    {
                        continue;
                    }

                    var name = FieldText(itemObj, ItemNameHints, ProjectHints);
                    if (string.IsNullOrWhiteSpace(name) || IsCodeValue(name))
                    {
                        continue;
                    }

                    items.Add(new TaxKindItemDto
                    {
                        Name = name,
                        Rate = RateOf(itemObj),
                        ValidFrom = NormalizeDate(FieldText(itemObj, ValidFromHints) ?? ""),
                        ValidTo = NormalizeDate(FieldText(itemObj, ValidToHints) ?? "")
                    });

                    if (items.Count >= MaxItemsPerRow)
                    {
                        return DedupItems(items);
                    }
                }
            }

            if (items.Count > 0)
            {
                return DedupItems(items);
            }

            // 一行里直接带征收品目的情况：把当前对象当成单条品目。
            var inlineName = FieldText(obj, ItemNameHints, ProjectHints);
            if (inlineName is { Length: > 0 } && !IsCodeValue(inlineName))
            {
                items.Add(new TaxKindItemDto
                {
                    Name = inlineName,
                    Rate = RateOf(obj),
                    ValidFrom = NormalizeDate(FieldText(obj, ValidFromHints) ?? ""),
                    ValidTo = NormalizeDate(FieldText(obj, ValidToHints) ?? "")
                });
            }

            return items;
        }

        private static List<TaxKindItemDto> DedupItems(List<TaxKindItemDto> items)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            return items
                .Where(item => seen.Add($"{item.Name}|{item.Rate}|{item.ValidFrom}|{item.ValidTo}"))
                .ToList();
        }

        /// <summary>
        /// 按提示词找字段值。同一个语义税局常常同时给两列——代码列 ZsxmDm 和名称列 Zsxmmc，
        /// 谁排在前面只是接口排版，按出现顺序取会把「30218」当成征收项目。所以逐条提示词找，
        /// 同一批命中里按语义打分：名称列优先，纯数字的代码列垫后。
        /// </summary>
        private static string? FieldText(JsonObject obj, string[] hints, string[]? excludes = null)
        {
            foreach (var hint in hints)
            {
                string? best = null;
                var bestScore = int.MinValue;
                foreach (var pair in obj)
                {
                    var key = NormalizeKey(pair.Key);
                    if (!key.Contains(hint, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (excludes is not null && excludes.Any(item => key.Contains(item, StringComparison.Ordinal)))
                    {
                        continue;
                    }

                    var text = TextOf(pair.Value);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        continue;
                    }

                    var value = text.Trim();
                    var isName = key.Contains("mc", StringComparison.Ordinal)
                        || key.Contains("名称", StringComparison.Ordinal)
                        || key.Contains("种类", StringComparison.Ordinal);
                    var score = (isName ? 2 : 0) - (IsCodeValue(value) ? 3 : 0);

                    if (score > bestScore)
                    {
                        bestScore = score;
                        best = value;
                    }
                }

                if (best is not null)
                {
                    return best;
                }
            }

            return null;
        }

        /// <summary>纯数字：税局的代码列长这样（30218、101017490），不是给纳税人看的名称。</summary>
        private static bool IsCodeValue(string text)
        {
            return text.Length >= 2 && text.All(char.IsDigit);
        }

        private static string? FindByValue(JsonObject obj, Func<string, bool> predicate)
        {
            foreach (var pair in obj)
            {
                var text = TextOf(pair.Value);
                if (!string.IsNullOrWhiteSpace(text) && predicate(text.Trim()))
                {
                    return text.Trim();
                }
            }

            return null;
        }

        private static string RateOf(JsonObject obj)
        {
            string? zeroRate = null;

            foreach (var pair in obj)
            {
                var key = NormalizeKey(pair.Key);
                if (!RateHints.Any(key.Contains))
                {
                    continue;
                }

                // 字段名里都含「sl」，但受理日期(slrq)、税率单位(sldw)、征收类型(zslx) 不是税率本身；
                // 「税率或单位税额」(slhdwse) 不以 dw 结尾，正是税局放税率的那一列。
                if (key.Contains("rq") || key.Contains("sj") || key.Contains("date")
                    || key.EndsWith("dw", StringComparison.Ordinal)
                    || key.Contains("lx") || key.Contains("lb") || key.Contains("type") || key.Contains("code"))
                {
                    continue;
                }

                var text = TextOf(pair.Value);
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                var value = text.Trim();
                // 主税那一列「税率」经常是 0（真实比例挂在「税率或单位税额」），有非零的优先用非零。
                if (IsZeroRate(value))
                {
                    zeroRate ??= value;
                    continue;
                }

                return value;
            }

            return zeroRate ?? "";
        }

        private static bool IsZeroRate(string text)
        {
            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
                && Math.Abs(number) < 1e-9;
        }

        private static string? TextOf(JsonNode? node)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text))
                {
                    return text;
                }

                if (value.TryGetValue<long>(out var number))
                {
                    return number.ToString();
                }

                if (value.TryGetValue<double>(out var decimalNumber))
                {
                    return decimalNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return null;
        }

        private static string NormalizeKey(string key)
        {
            return key.Replace("_", "").Replace("-", "").Trim().ToLowerInvariant();
        }

        private static string NormalizePeriod(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "";
            }

            var text = raw.Trim();
            if (text.Contains('季'))
            {
                return "季";
            }

            if (text.Contains('月'))
            {
                return "月";
            }

            if (text.Contains('年'))
            {
                return "年";
            }

            if (text.Contains('次'))
            {
                return "次";
            }

            return text;
        }

        private static string NormalizeDate(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "";
            }

            var match = Regex.Match(raw, @"(\d{4})\D{0,2}(\d{1,2})\D{0,2}(\d{1,2})");
            if (!match.Success)
            {
                return raw.Trim();
            }

            if (!int.TryParse(match.Groups[1].Value, out var year)
                || !int.TryParse(match.Groups[2].Value, out var month)
                || !int.TryParse(match.Groups[3].Value, out var day)
                || month is < 1 or > 12
                || day is < 1 or > 31)
            {
                return raw.Trim();
            }

            return $"{year:D4}-{month:D2}-{day:D2}";
        }

        private static List<TaxKindRowDto> Dedup(List<TaxKindRowDto> rows)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<TaxKindRowDto>();
            foreach (var row in rows)
            {
                var itemsKey = row.Items is null
                    ? ""
                    : string.Join(",", row.Items.Select(item => $"{item.Name}@{item.Rate}@{item.ValidFrom}@{item.ValidTo}"));
                var key = $"{row.Project}|{row.DeclareKind}|{row.Period}|{row.Option}|{row.ValidFrom}|{row.ValidTo}|{itemsKey}";
                if (!seen.Add(key))
                {
                    continue;
                }

                result.Add(new TaxKindRowDto
                {
                    Key = $"tax-{result.Count}",
                    Project = row.Project,
                    DeclareKind = row.DeclareKind,
                    Period = row.Period,
                    Option = row.Option,
                    ValidFrom = row.ValidFrom,
                    ValidTo = row.ValidTo,
                    Source = row.Source,
                    Items = row.Items
                });
            }

            return result;
        }

        private static string Shorten(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "(未知地址)";
            }

            var index = url.IndexOf('?');
            var trimmed = index > 0 ? url[..index] : url;
            return trimmed.Length <= 120 ? trimmed : trimmed[..120] + "…";
        }
    }
}
