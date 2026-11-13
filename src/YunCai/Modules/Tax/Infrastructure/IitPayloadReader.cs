using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 个税（自然人电子税务局）响应解析共用的取值工具：接口字段名有拼音缩写与中文键两套风格，
    /// 且各省前置系统可能再包一层，所以统一按「候选字段名」取值，并把命中的数组递归找出来。
    /// </summary>
    internal static class IitPayloadReader
    {
        internal static readonly JsonSerializerOptions RawJsonOptions = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>证件类型编码 → 名称（个税系统的通用码表；未识别时保留原值）。</summary>
        private static readonly Dictionary<string, string> IdTypeNames = new(StringComparer.Ordinal)
        {
            ["1"] = "居民身份证",
            ["2"] = "军官证",
            ["3"] = "士兵证",
            ["4"] = "武警警官证",
            ["5"] = "港澳居民来往内地通行证",
            ["6"] = "外交人员身份证件",
            ["7"] = "外国人永久居留身份证",
            ["8"] = "台湾居民来往大陆通行证",
            ["9"] = "护照",
            ["10"] = "港澳台居民居住证",
        };

        /// <summary>递归收集「元素是对象」的数组，深度限制防止把整棵响应树翻到底。</summary>
        internal static void CollectArrays(JsonNode node, List<JsonArray> arrays, int depth)
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

        /// <summary>按候选字段名取第一个非空字符串值。</summary>
        internal static string PickText(JsonObject obj, IReadOnlyList<string> hints)
        {
            foreach (var hint in hints)
            {
                if (obj.TryGetPropertyValue(hint, out var value))
                {
                    var text = ValueToString(value);
                    if (text.Length > 0)
                    {
                        return text;
                    }
                }
            }

            return string.Empty;
        }

        /// <summary>取日期并截到 yyyy-MM-dd；1900-01-01 是个税侧「无该日期」的占位值。</summary>
        internal static string PickDate(JsonObject obj, IReadOnlyList<string> hints)
        {
            var text = PickText(obj, hints);
            if (text.Length == 0 || text.StartsWith("1900", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            return text.Length >= 10 ? text[..10] : text;
        }

        /// <summary>把布尔/0-1/「是否」几种写法统一成「是」或「否」，取不到返回空串。</summary>
        internal static string PickBoolText(JsonObject obj, IReadOnlyList<string> hints)
        {
            foreach (var hint in hints)
            {
                if (!obj.TryGetPropertyValue(hint, out var value) || value is null)
                {
                    continue;
                }

                if (value.GetValueKind() == JsonValueKind.True)
                {
                    return "是";
                }

                if (value.GetValueKind() == JsonValueKind.False)
                {
                    return "否";
                }

                var text = ValueToString(value);
                if (text is "1" or "true" or "是")
                {
                    return "是";
                }

                if (text is "0" or "false" or "否")
                {
                    return "否";
                }
            }

            return string.Empty;
        }

        internal static string ValueToString(JsonNode? value)
        {
            if (value is null || value.GetValueKind() is JsonValueKind.Null)
            {
                return string.Empty;
            }

            if (value.GetValueKind() is JsonValueKind.String)
            {
                return value.GetValue<string>().Trim();
            }

            return value.ToJsonString().Trim().Trim('"');
        }

        /// <summary>取金额：兼容 1234.5、"1,234.50" 与数字字符串三种写法，取不到返回 0。</summary>
        internal static decimal PickDecimal(JsonObject obj, IReadOnlyList<string> hints)
        {
            var text = PickText(obj, hints);
            if (text.Length == 0)
            {
                return 0m;
            }

            var cleaned = text.Replace(",", string.Empty).Replace("元", string.Empty).Trim();
            return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0m;
        }

        internal static string MapIdType(string raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            return IdTypeNames.TryGetValue(raw, out var name) ? name : raw;
        }

        /// <summary>按 Key 去重，保留首次出现的行。</summary>
        internal static List<T> DedupBy<T>(List<T> rows, Func<T, string> keyOf)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<T>();
            foreach (var row in rows)
            {
                if (seen.Add(keyOf(row)))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        /// <summary>诊断信息里的接口地址只留路径与查询串，避免整行过长。</summary>
        internal static string Shorten(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                url = uri.PathAndQuery;
            }

            return url.Length <= 90 ? url : url[..90];
        }

        /// <summary>解析单个响应体；解析不出行时回传候选数组的字段名，供补解析规则。</summary>
        internal static List<JsonArray> ArraysOf(string body)
        {
            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                return [];
            }

            if (root is null)
            {
                return [];
            }

            var arrays = new List<JsonArray>();
            CollectArrays(root, arrays, 0);
            return arrays;
        }
    }
}
