using System.Globalization;
using System.Text.Json.Nodes;
using KuaiJi.Modules.Tax.Application;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 解析电子税务局数字账户接口回传的缴款记录与已开具完税证明。
    ///
    /// 报文固定为 {"Response":{"Data":{"List":[...]}}}，但各省字段命名不完全一致，
    /// 所以不按字段位置取值，而是递归找对象数组、按候选字段名认；
    /// 拼音缩写（skssqq / sjje / jkrq）与中文列名两套都认，多出来的汇总数组由锚字段挡掉。
    /// </summary>
    internal static class TaxPaymentPayloadParser
    {
        private const int MaxRows = 5000;

        // 表格行必须带「征收项目」这类锚，否则会把菜单、合计行、申报表也当成缴款记录。
        private static readonly string[] ProjectHints =
            ["征收项目", "税种", "税费种", "税费种名称", "征收项目名称", "zsxm", "zsxmMc", "zsxm_mc"];

        private static readonly string[] ItemHints =
            ["征收品目", "品目", "征收品目名称", "zspm", "zspmMc", "zspm_mc"];

        private static readonly string[] PeriodStartHints =
            ["税款所属期起", "所属期起", "税款所属期开始", "起始所属期", "skssqq", "skssqqStr", "ssqsqq"];

        private static readonly string[] PeriodEndHints =
            ["税款所属期止", "所属期止", "税款所属期结束", "终止所属期", "skssqz", "skssqzStr", "sssqz"];

        private static readonly string[] PeriodHints =
            ["税款所属期", "所属期", "税款所属月份", "税款所属期起止", "skssq", "sssq"];

        private static readonly string[] AmountHints =
            ["实缴（退）金额", "实缴(退)金额", "实缴金额", "实缴税额", "缴款金额", "入库金额", "开具金额", "金额",
             "sjje", "jkje", "kjje", "rkrqje", "zse", "se"];

        private static readonly string[] TaxKindHints =
            ["税款种类", "税费种类", "种类", "正税滞纳金罚款", "skzl", "skzlMc", "skzl_mc"];

        private static readonly string[] PaidOnHints =
            ["缴款日期", "入库日期", "入（退）库日期", "入(退)库日期", "解缴日期", "缴款时间", "入库时间",
             "jkrq", "rkrq", "jkrqStr", "rkrqStr"];

        private static readonly string[] MethodHints =
            ["税费缴纳方式", "缴款方式", "缴纳方式", "缴费方式", "税款缴纳方式",
             "jkfs", "jkfsMc", "jkfs_mc", "skjnfs", "skjnfsMc"];

        private static readonly string[] VoucherKindHints =
            ["票证种类", "凭证种类", "票证类型", "pzzl", "pzzlMc", "pzzl_mc"];

        private static readonly string[] VoucherNoHints =
            ["原凭证号", "票证号码", "凭证号码", "缴款书号", "税票号码", "pzh", "pzhm", "pzhhm", "ypzh", "voucherNo"];

        private static readonly string[] ETicketNoHints =
            ["电子税票号码", "电子税票号", "电子缴款书号", "dzsphm", "dzsph"];

        private static readonly string[] CertificateNameHints =
            ["证明名称", "证明种类", "zmmx"];

        private static readonly string[] AuthorityHints =
            ["开具税务机关", "税务机关", "kjswjgMc", "swjgMc"];

        private static readonly string[] IssuedOnHints =
            ["开具日期", "开具时间", "开票日期", "证明开具日期", "kjrq", "kjrqStr", "kprq"];

        private static readonly string[] FormatHints =
            ["开具格式", "证明格式", "证明样式", "格式", "开具方式",
             "kjgs", "kjgsMc", "kjgs_mc", "pzzl", "pzzlMc"];

        private static readonly string[] IdHints =
            ["sbuuid", "pzxh", "djxh", "id", "uuid", "key"];

        public static TaxPaymentsResultDto ParsePayments(
            IReadOnlyList<CapturedJsonPayload> payloads,
            DateOnly from,
            DateOnly to)
        {
            var notes = new List<string>();
            var candidates = Collect(payloads, obj => ParsePaymentRow(obj), notes);
            var rows = candidates
                .Where(row => WithinRange(row.PaidOn, from, to))
                .ToList();

            if (rows.Count == 0 && candidates.Count > 0)
            {
                notes.Add($"解析到 {candidates.Count} 条候选记录，缴款日期都不在所选区间内。");
            }

            rows.Sort((left, right) => string.CompareOrdinal(right.PaidOn, left.PaidOn));
            rows = Dedup(rows, row => row.Key);

            return new TaxPaymentsResultDto
            {
                Rows = rows,
                Message = rows.Count > 0 ? null : "所选缴款日期区间没有缴款记录。",
                Diagnostics = rows.Count > 0 ? null : notes.Take(20).ToList()
            };
        }

        public static TaxCertificatesResultDto ParseCertificates(
            IReadOnlyList<CapturedJsonPayload> payloads,
            DateOnly from,
            DateOnly to)
        {
            var notes = new List<string>();
            var candidates = Collect(payloads, obj => ParseCertificateRow(obj), notes);
            var rows = candidates
                .Where(row => WithinRange(row.IssuedOn, from, to))
                .ToList();

            if (rows.Count == 0 && candidates.Count > 0)
            {
                notes.Add($"解析到 {candidates.Count} 条候选记录，开具日期都不在所选区间内。");
            }

            rows.Sort((left, right) => string.CompareOrdinal(right.IssuedOn, left.IssuedOn));
            rows = Dedup(rows, row => row.Key);

            return new TaxCertificatesResultDto
            {
                Rows = rows,
                Message = rows.Count > 0 ? null : "所选开具日期区间没有已开具的完税证明。",
                Diagnostics = rows.Count > 0 ? null : notes.Take(20).ToList()
            };
        }

        /// <summary>
        /// 把一份报文里的所有对象数组交给同一个行解析器，解析不出时把候选数组的字段名留作诊断。
        /// 同一个响应里常夹着分页、合计、下拉选项等数组，行解析器用锚字段把它们挡掉。
        /// </summary>
        private static List<T> Collect<T>(
            IReadOnlyList<CapturedJsonPayload> payloads,
            Func<JsonObject, T?> parse,
            List<string> notes) where T : class
        {
            var rows = new List<T>();
            foreach (var payload in payloads)
            {
                var arrays = IitPayloadReader.ArraysOf(payload.Body);
                if (arrays.Count == 0)
                {
                    continue;
                }

                var sample = string.Empty;
                var parsed = 0;
                foreach (var array in arrays)
                {
                    foreach (var item in array.OfType<JsonObject>())
                    {
                        var row = parse(item);
                        if (row is not null)
                        {
                            rows.Add(row);
                            parsed++;
                            if (rows.Count >= MaxRows)
                            {
                                return rows;
                            }
                        }
                        else if (sample.Length == 0 && item.Count > 0)
                        {
                            sample = string.Join("|", item.Select(pair => pair.Key).Take(16));
                        }
                    }
                }

                if (parsed == 0 && sample.Length > 0)
                {
                    notes.Add($"{IitPayloadReader.Shorten(payload.Url)} 字段={sample}");
                }
            }

            return rows;
        }

        private static TaxPaymentRowDto? ParsePaymentRow(JsonObject obj)
        {
            var project = IitPayloadReader.PickText(obj, ProjectHints);
            var paidOn = Date(obj, PaidOnHints);
            // 缴款记录以「征收项目 + 缴款日期」为锚：申报表、菜单这类表格没有缴款日期，挡在门外。
            if (project.Length == 0 || paidOn.Length == 0)
            {
                return null;
            }

            var (periodStart, periodEnd) = ReadPeriod(obj);
            var amount = Amount(obj, AmountHints);
            var voucherNo = IitPayloadReader.PickText(obj, VoucherNoHints);
            var eTicketNo = IitPayloadReader.PickText(obj, ETicketNoHints);
            var method = IitPayloadReader.PickText(obj, MethodHints);
            var taxKind = IitPayloadReader.PickText(obj, TaxKindHints);
            var item = IitPayloadReader.PickText(obj, ItemHints);

            return new TaxPaymentRowDto
            {
                Key = RowKey(obj, project, periodStart, periodEnd, paidOn, amount.ToString(CultureInfo.InvariantCulture), voucherNo),
                Project = project,
                Item = item,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Amount = amount,
                TaxKind = taxKind,
                PaidOn = paidOn,
                Method = method,
                VoucherKind = IitPayloadReader.PickText(obj, VoucherKindHints),
                VoucherNo = voucherNo,
                ETicketNo = eTicketNo
            };
        }

        private static TaxCertificateRowDto? ParseCertificateRow(JsonObject obj)
        {
            var voucherNo = IitPayloadReader.PickText(obj, VoucherNoHints);
            var issuedOn = Date(obj, IssuedOnHints);
            var paidOn = Date(obj, PaidOnHints);
            var format = IitPayloadReader.PickText(obj, FormatHints);
            var name = IitPayloadReader.PickText(obj, CertificateNameHints);

            // 票证号码（或证明名称）+ 开具日期/格式才是一张完税证明；缴款明细同样有票证号码与入库日期，不能混进来。
            if ((voucherNo.Length == 0 && name.Length == 0) || (issuedOn.Length == 0 && format.Length == 0))
            {
                return null;
            }

            var (periodStart, periodEnd) = ReadPeriod(obj);
            return new TaxCertificateRowDto
            {
                Key = RowKey(obj, voucherNo, periodStart, periodEnd, paidOn, issuedOn, format),
                Name = name,
                VoucherNo = voucherNo,
                Authority = IitPayloadReader.PickText(obj, AuthorityHints),
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                PaidOn = paidOn,
                Amount = Amount(obj, AmountHints),
                IssuedOn = issuedOn,
                Format = format
            };
        }

        /// <summary>
        /// 所属期有起止两列时就分开读；只有一列「2026-01-01 至 2026-01-31」时按「至」拆开。
        /// 只给到月份的（2026-01）原样保留，页面显示不会比税局更细。
        /// </summary>
        private static (string Start, string End) ReadPeriod(JsonObject obj)
        {
            var start = Date(obj, PeriodStartHints);
            var end = Date(obj, PeriodEndHints);
            if (start.Length > 0 || end.Length > 0)
            {
                return (start, end);
            }

            var text = IitPayloadReader.PickText(obj, PeriodHints);
            if (text.Length == 0)
            {
                return (string.Empty, string.Empty);
            }

            foreach (var separator in new[] { '至', '~', '—' })
            {
                var index = text.IndexOf(separator);
                if (index <= 0)
                {
                    continue;
                }

                var left = FuzzDate(text[..index]);
                var right = FuzzDate(text[(index + 1)..]);
                if (left.Length > 0 && right.Length > 0)
                {
                    return (left, right);
                }
            }

            // 「2026-01」里的连字符是年月分隔符，不是区间分隔符：两边都得像完整日期才按区间拆。
            if (text.Count(character => character == '-') == 1)
            {
                var index = text.IndexOf('-');
                var left = FuzzDate(text[..index]);
                var right = FuzzDate(text[(index + 1)..]);
                if (left.Length > 0 && right.Length > 0
                    && text[..index].Count(char.IsDigit) >= 6
                    && text[(index + 1)..].Count(char.IsDigit) >= 6)
                {
                    return (left, right);
                }
            }

            var single = FuzzDate(text);
            return single.Length > 0
                ? (single, single)
                : (string.Empty, string.Empty);
        }

        private static string RowKey(JsonObject obj, params string[] parts)
        {
            var id = IitPayloadReader.PickText(obj, IdHints);
            if (id.Length > 0)
            {
                return id;
            }

            return string.Join("|", parts);
        }

        /// <summary>读日期并归一成 yyyy-MM-dd；只有月份的保留 yyyy-MM，1900 占位日期视为空。</summary>
        private static string Date(JsonObject obj, IReadOnlyList<string> hints)
        {
            var text = IitPayloadReader.PickText(obj, hints);
            if (text.Length == 0 || text.StartsWith("1900", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var normalized = FuzzDate(text);
            return normalized.Length > 0
                ? normalized
                : (text.Length >= 10 ? text[..10] : text);
        }

        private static List<T> Dedup<T>(List<T> rows, Func<T, string> keyOf)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<T>(rows.Count);
            foreach (var row in rows)
            {
                if (seen.Add(keyOf(row)))
                {
                    result.Add(row);
                }
            }

            return result;
        }

        /// <summary>金额：兼容 1,234.50、¥1,234.50、全角括号负数和 (123.45) 这种会计负数写法。</summary>
        private static decimal Amount(JsonObject obj, IReadOnlyList<string> hints)
        {
            var text = IitPayloadReader.PickText(obj, hints);
            return ParseAmount(text);
        }

        private static decimal ParseAmount(string text)
        {
            if (text.Length == 0)
            {
                return 0m;
            }

            var negative = (text.StartsWith('(') && text.EndsWith(')'))
                || (text.StartsWith('（') && text.EndsWith('）'));
            var cleaned = text.Trim('(', ')', '（', '）', '¥', '￥', '元', ' ');
            cleaned = cleaned.Replace(",", string.Empty).Replace("，", string.Empty).Trim();
            if (cleaned.Length == 0)
            {
                return 0m;
            }

            if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value))
            {
                return 0m;
            }

            return negative ? -value : value;
        }

        private static string FuzzDate(string text)
        {
            var trimmed = text.Trim();
            if (trimmed.Length == 0)
            {
                return string.Empty;
            }

            // 中文日期（2026年1月15日）、带时间的字符串、2026-01 这类只有月份的写法都交给中文区域解析。
            if (DateTime.TryParse(trimmed, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.None, out var parsed))
            {
                return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            // 20260115 这种纯数字日期 DateTime 也能认，但先按 8 位数字兜一手，避免被当成别的数。
            var digits = new string(trimmed.Where(char.IsDigit).ToArray());
            if (digits.Length == 8
                && DateOnly.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
            {
                return day.ToString("yyyy-MM-dd");
            }

            if (digits.Length == 6
                && int.TryParse(digits[..4], out var year)
                && int.TryParse(digits[4..], out var month)
                && month is >= 1 and <= 12)
            {
                return $"{year:D4}-{month:D2}";
            }

            return string.Empty;
        }

        private static bool WithinRange(string date, DateOnly from, DateOnly to)
        {
            if (date.Length < 10
                || !DateOnly.TryParseExact(date[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                // 日期解析不出的行不能直接丢：税局偶尔只回「2026-01」这种月份，
                // 宁可多展示一行，也不要让会计以为没缴过。
                return true;
            }

            return parsed >= from && parsed <= to;
        }
    }
}
