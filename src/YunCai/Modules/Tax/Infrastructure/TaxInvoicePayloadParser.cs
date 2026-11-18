using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using YunCai.Modules.Invoices.Domain;
using YunCai.Modules.Tax.Application;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把「全量发票查询」页面背后的 JSON 响应解析成发票票头。
    ///
    /// 各省电子税务局的字段名并不统一（有的用拼音缩写 fphm/kprq/jshj，有的用中文键），
    /// 所以按「字段名语义 + 取值特征」做启发式映射。发票方向不靠页面页签猜，而是拿
    /// 购销方纳税人识别号与本企业税号比对：销方是自己即销项，购方是自己即进项。
    /// 解析不到时回传候选数组的字段名，便于按真实响应补充规则。
    /// </summary>
    internal static class TaxInvoicePayloadParser
    {
        private const int MaxRows = 5000;

        private static readonly string[] InvoiceNoHints =
            ["fphm", "fpno", "invoiceno", "invoicenumber", "发票号码", "票号"];

        private static readonly string[] IssueDateHints =
            ["kprq", "kprqstr", "kpsj", "issuedate", "invoicedate", "开票日期", "开票时间"];

        private static readonly string[] InvoiceTypeHints =
            ["fplxmc", "fpzlmc", "fplx", "fpzl", "invoicetype", "发票种类", "发票类型", "票种"];

        private static readonly string[] BuyerNameHints =
            ["gmfmc", "gmfname", "gfmc", "gfname", "buyername", "购买方名称", "购方名称"];

        private static readonly string[] BuyerTaxNoHints =
            ["gmfnsrsbh", "gmfsbh", "gmfsh", "gmfnsr", "gfnsrsbh", "gfsh", "buyertaxno", "购买方纳税人识别号", "购方纳税人识别号", "购买方识别号", "购方识别号"];

        private static readonly string[] SellerNameHints =
            ["xsfmc", "xsfname", "xfmc", "xfname", "sellername", "销售方名称", "销方名称"];

        private static readonly string[] SellerTaxNoHints =
            ["xsfnsrsbh", "xsfsh", "xsfnsr", "xfnsrsbh", "xfsh", "sellertaxno", "销售方纳税人识别号", "销方纳税人识别号", "销售方识别号", "销方识别号"];

        private static readonly string[] AmountHints = ["hjje", "je", "hemje", "amount", "合计金额", "金额"];

        private static readonly string[] TaxHints = ["hjse", "se", "taxamount", "合计税额", "税额"];

        private static readonly string[] TotalHints = ["jshj", "jshjje", "totalwithtax", "价税合计"];

        private static readonly string[] GoodsHints =
            ["hwmc", "spmc", "goodsname", "货物或应税劳务名称", "商品名称", "货物名称"];

        /// <summary>票种代码 → 名称；税局只回代码时兜底，回中文名时保留原样。</summary>
        private static readonly Dictionary<string, string> InvoiceTypeCodes = new(StringComparer.Ordinal)
        {
            ["01"] = "增值税专用发票",
            ["02"] = "货物运输业增值税专用发票",
            ["03"] = "机动车销售统一发票",
            ["04"] = "增值税普通发票",
            ["08"] = "增值税电子专用发票",
            ["10"] = "增值税电子普通发票",
            ["11"] = "增值税普通发票（卷式）",
            ["14"] = "增值税电子普通发票（通行费）",
            ["15"] = "二手车销售统一发票",
            ["31"] = "数电发票（增值税专用发票）",
            ["32"] = "数电发票（普通发票）",
            ["33"] = "数电发票（机动车销售统一发票）",
            ["34"] = "数电发票（二手车销售统一发票）",
            ["35"] = "数电发票（航空运输电子客票行程单）",
            ["36"] = "数电发票（铁路电子客票）",
            ["37"] = "数电发票（通行费发票）",
        };

        public static IReadOnlyList<TaxInvoiceRecord> Parse(
            IReadOnlyList<CapturedJsonPayload> payloads,
            string ownTaxpayerNumber,
            out IReadOnlyList<string> diagnostics)
        {
            var rows = new List<TaxInvoiceRecord>();
            var notes = new List<string>();
            var own = NormalizeTaxNo(ownTaxpayerNumber);

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

                var arrays = new List<JsonArray>();
                CollectArrays(root, arrays, 0);
                var before = rows.Count;
                var sample = "";
                foreach (var array in arrays)
                {
                    ParseArray(array, own, rows);
                    if (sample.Length == 0 && array.Count > 0 && array[0] is JsonObject first)
                    {
                        sample = string.Join("|", first.Select(pair => pair.Key).Take(12));
                    }

                    if (rows.Count >= MaxRows)
                    {
                        break;
                    }
                }

                if (rows.Count == before)
                {
                    // 没解析出任何一张发票：既要区分「税局确实回了 0 条」和「字段/结构对不上」，
                    // 也要把真实字段名或整体结构带回去，否则只能靠用户再跑一遍。
                    var zero = IsZeroResult(root);
                    var detail = sample.Length > 0 ? $"字段={sample}" : $"结构={DescribeShape(root)}";
                    notes.Add($"{Shorten(payload.Url)}{(zero ? " 返回 0 条" : "")} {detail}");
                }
            }

            var deduped = Dedup(rows);
            diagnostics = deduped.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return deduped;
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
                    // 有的接口把业务报文再包成字符串：{"data":"{\"list\":[...]}"}，先解开再收数组。
                    if (pair.Value is JsonValue value
                        && value.TryGetValue<string>(out var text)
                        && TryParseEmbeddedJson(text, out var embedded)
                        && embedded is not null)
                    {
                        CollectArrays(embedded, arrays, depth + 1);
                        continue;
                    }

                    if (pair.Value is not null)
                    {
                        CollectArrays(pair.Value, arrays, depth + 1);
                    }
                }
            }
        }

        private static bool TryParseEmbeddedJson(string text, out JsonNode? node)
        {
            node = null;
            var trimmed = text.TrimStart();
            if (trimmed.Length < 2 || (trimmed[0] != '{' && trimmed[0] != '['))
            {
                return false;
            }

            try
            {
                node = JsonNode.Parse(trimmed);
                return node is not null;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>把响应压成一行结构摘要：键名 + 长度/类型，字符串只留开头。</summary>
        private static string DescribeShape(JsonNode? node) => DescribeShape(node, 0);

        private static string DescribeShape(JsonNode? node, int depth)
        {
            if (node is null)
            {
                return "null";
            }

            if (node is JsonArray array)
            {
                return array.Count == 0
                    ? "[]"
                    : depth >= 2
                        ? $"[{array.Count}]"
                        : $"[{array.Count}]{DescribeShape(array[0], depth + 1)}";
            }

            if (node is JsonObject obj)
            {
                if (depth >= 3)
                {
                    return $"{{…{obj.Count} 项}}";
                }

                var parts = obj.Take(10).Select(pair => $"{pair.Key}:{DescribeShape(pair.Value, depth + 1)}");
                return "{" + string.Join(",", parts) + (obj.Count > 10 ? $",…{obj.Count} 项" : "") + "}";
            }

            var text = TextOf(node) ?? string.Empty;
            return text.Length > 16 ? $"\"{text[..16]}…\"" : $"\"{text}\"";
        }

        /// <summary>查询接口明确回了 0 条：合计字段为 0，或结果列表为空。</summary>
        private static bool IsZeroResult(JsonNode? node, int depth = 0)
        {
            if (node is not JsonObject obj || depth > 5)
            {
                return false;
            }

            foreach (var pair in obj)
            {
                var key = NormalizeKey(pair.Key);
                if ((key.Contains("total") || key is "count" or "zts" or "totalnum" or "totalrecord")
                    && TextOf(pair.Value) == "0")
                {
                    return true;
                }
            }

            foreach (var pair in obj)
            {
                if (pair.Value is JsonArray array)
                {
                    if (array.Count == 0
                        && NormalizeKey(pair.Key) is "list" or "rows" or "records" or "items" or "result" or "data")
                    {
                        return true;
                    }
                }
                else if (pair.Value is JsonObject child && IsZeroResult(child, depth + 1))
                {
                    return true;
                }
            }

            return false;
        }

        private static void ParseArray(JsonArray array, string own, List<TaxInvoiceRecord> rows)
        {
            foreach (var node in array)
            {
                if (node is not JsonObject obj)
                {
                    continue;
                }

                var record = ParseObject(obj, own);
                if (record is not null)
                {
                    rows.Add(record);
                    if (rows.Count >= MaxRows)
                    {
                        return;
                    }
                }
            }
        }

        private static TaxInvoiceRecord? ParseObject(JsonObject obj, string own)
        {
            var invoiceNo = FieldText(obj, InvoiceNoHints);
            if (invoiceNo is null || invoiceNo.Length < 6 || !invoiceNo.Any(char.IsDigit))
            {
                return null;
            }

            var issueDate = ParseDate(FieldText(obj, IssueDateHints));
            if (issueDate is null)
            {
                return null;
            }

            var total = ParseMoney(FieldText(obj, TotalHints, ["金额"]));
            var amount = ParseMoney(FieldText(obj, AmountHints, ["jshj", "价税合计", "税额"]));
            var tax = ParseMoney(FieldText(obj, TaxHints, ["jshj", "价税合计", "金额"]));

            // 合计金额缺失时用价税合计倒推；两者都没有说明这行不是发票。
            if (amount is null && total is null)
            {
                return null;
            }

            if (amount is null)
            {
                amount = (total ?? 0m) - (tax ?? 0m);
            }

            tax ??= 0m;

            var direction = ResolveDirection(obj, own);
            if (direction is null)
            {
                return null;
            }

            var partnerName = direction == InvoiceDirection.Sales
                ? FieldText(obj, BuyerNameHints)
                : FieldText(obj, SellerNameHints);
            if (string.IsNullOrWhiteSpace(partnerName))
            {
                partnerName = direction == InvoiceDirection.Sales
                    ? FieldText(obj, SellerNameHints)
                    : FieldText(obj, BuyerNameHints);
            }

            var raw = obj.ToJsonString();
            return new TaxInvoiceRecord(
                direction.Value,
                ResolveInvoiceType(FieldText(obj, InvoiceTypeHints)),
                issueDate.Value,
                invoiceNo,
                partnerName ?? string.Empty,
                amount.Value,
                tax.Value,
                FieldText(obj, GoodsHints),
                raw.Length <= 2000 ? raw : raw[..2000]);
        }

        private static InvoiceDirection? ResolveDirection(JsonObject obj, string own)
        {
            if (own.Length == 0)
            {
                return null;
            }

            var seller = NormalizeTaxNo(FieldText(obj, SellerTaxNoHints));
            var buyer = NormalizeTaxNo(FieldText(obj, BuyerTaxNoHints));
            if (seller.Length > 0 && seller == own)
            {
                return InvoiceDirection.Sales;
            }

            if (buyer.Length > 0 && buyer == own)
            {
                return InvoiceDirection.Purchase;
            }

            // 字段名认不出来时（浙江数电用 gmf/xsf 一类缩写），直接找值等于本企业税号的字段，
            // 再按键名判断是购方还是销方；猜不出就返回 null，宁可不入库也不写错方向。
            foreach (var pair in obj)
            {
                if (NormalizeTaxNo(TextOf(pair.Value)) != own)
                {
                    continue;
                }

                var key = NormalizeKey(pair.Key);
                if (key.Contains("gmf") || key.Contains("buyer") || key.Contains("购"))
                {
                    return InvoiceDirection.Purchase;
                }

                if (key.Contains("xsf") || key.Contains("seller") || key.Contains("销"))
                {
                    return InvoiceDirection.Sales;
                }
            }

            return null;
        }

        private static string ResolveInvoiceType(string? raw)
        {
            var text = raw?.Trim() ?? string.Empty;
            if (text.Length == 0)
            {
                return "其他发票";
            }

            if (text.Any(c => c > 127))
            {
                return text;
            }

            return InvoiceTypeCodes.TryGetValue(text, out var name) ? name : "其他发票";
        }

        private static string? FieldText(JsonObject obj, string[] hints, string[]? excludedContains = null)
        {
            // 先精确命中，再退到包含匹配：je 不该先吃掉 jshjje，se 也不该吃掉别的字段。
            foreach (var hint in hints)
            {
                foreach (var pair in obj)
                {
                    var key = NormalizeKey(pair.Key);
                    if (key != hint)
                    {
                        continue;
                    }

                    if (excludedContains is not null && excludedContains.Any(key.Contains))
                    {
                        continue;
                    }

                    var text = TextOf(pair.Value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }

            foreach (var hint in hints)
            {
                foreach (var pair in obj)
                {
                    var key = NormalizeKey(pair.Key);
                    if (!key.Contains(hint))
                    {
                        continue;
                    }

                    if (excludedContains is not null && excludedContains.Any(key.Contains))
                    {
                        continue;
                    }

                    var text = TextOf(pair.Value);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text.Trim();
                    }
                }
            }

            return null;
        }

        private static string? TextOf(JsonNode? node)
        {
            if (node is JsonValue value)
            {
                if (value.TryGetValue<string>(out var text))
                {
                    return text;
                }

                if (value.TryGetValue<decimal>(out var number))
                {
                    return number.ToString(CultureInfo.InvariantCulture);
                }

                if (value.TryGetValue<long>(out var integer))
                {
                    return integer.ToString(CultureInfo.InvariantCulture);
                }
            }

            return null;
        }

        private static decimal? ParseMoney(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var text = raw.Trim()
                .Replace(",", "", StringComparison.Ordinal)
                .Replace("，", "", StringComparison.Ordinal)
                .Replace("¥", "", StringComparison.Ordinal)
                .Replace("￥", "", StringComparison.Ordinal)
                .Replace(" ", "", StringComparison.Ordinal);
            if (text is "-" or "—" or "/" or "")
            {
                return null;
            }

            return decimal.TryParse(text, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
                ? value
                : null;
        }

        private static DateOnly? ParseDate(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var match = Regex.Match(raw, @"(\d{4})\D{0,2}(\d{1,2})\D{0,2}(\d{1,2})");
            if (!match.Success
                || !int.TryParse(match.Groups[1].Value, out var year)
                || !int.TryParse(match.Groups[2].Value, out var month)
                || !int.TryParse(match.Groups[3].Value, out var day)
                || month is < 1 or > 12
                || day is < 1 or > 31)
            {
                return null;
            }

            return new DateOnly(year, month, day);
        }

        private static string NormalizeTaxNo(string? raw)
            => string.IsNullOrWhiteSpace(raw)
                ? string.Empty
                : new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

        private static string NormalizeKey(string key)
            => key.Replace("_", "", StringComparison.Ordinal)
                .Replace("-", "", StringComparison.Ordinal)
                .Trim()
                .ToLowerInvariant();

        private static List<TaxInvoiceRecord> Dedup(List<TaxInvoiceRecord> rows)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<TaxInvoiceRecord>();
            foreach (var row in rows)
            {
                if (seen.Add($"{row.Direction}|{row.InvoiceNo}"))
                {
                    result.Add(row);
                }
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
