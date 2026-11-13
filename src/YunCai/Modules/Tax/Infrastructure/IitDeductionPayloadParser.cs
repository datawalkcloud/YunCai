using System.Text.Json.Nodes;
using YunCai.Modules.Tax.Application;

namespace YunCai.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把个税（自然人电子税务局）专项附加扣除相关 JSON 响应解析成扣除行。
    ///
    /// 与人员信息同一思路：按「字段名语义 + 取值特征」做启发式映射，不绑定固定 DTO。
    /// 判定一行是不是扣除记录，以「扣除项目能识别出来」为准——只有姓名没有项目的数组
    /// 通常是别的主数据（如人员列表本身），不能混进来。解析不到时回传候选数组字段名补规则。
    /// </summary>
    internal static class IitDeductionPayloadParser
    {
        private const int MaxRows = 2000;

        private static readonly string[] NameHints =
            ["e_name", "xm", "xingming", "name", "cname", "employee_name", "person_name", "姓名"];

        private static readonly string[] IdNoHints =
            ["e_id", "sfz", "zjhm", "idcard", "id_card", "idno", "id_no", "cardid", "id_number", "证件号码", "证件号", "身份证号"];

        private static readonly string[] IdTypeHints =
            ["e_type", "zjlx", "id_type", "idtype", "证件类型"];

        private static readonly string[] ProjectHints =
        [
            "kclx", "kc_xm", "cplx", "special_deduction_type", "deduction_type", "deduction_project",
            "project_name", "扣除项目", "扣除类型", "专项附加扣除项目", "项目类型",
        ];

        private static readonly string[] RelativeHints =
        [
            "gx", "relation", "relationship", "family_relation", "kydx", "扣除对象", "关系", "与被赡养人关系", "子女",
        ];

        private static readonly string[] YearHints =
            ["kcnd", "nd", "year", "deduction_year", "年度", "扣除年度"];

        private static readonly string[] PeriodStartHints =
            ["yxqq", "qsrq", "begin_date", "start_date", "valid_start", "有效期起", "起始时间", "开始时间"];

        private static readonly string[] PeriodEndHints =
            ["yxqz", "zzrq", "end_date", "valid_end", "expire_date", "有效期止", "截止时间", "结束时间"];

        private static readonly string[] AmountHints =
            ["jehz", "kcje", "amount", "deduction_amount", "monthly_amount", "std", "扣除金额", "每月扣除金额", "扣除标准"];

        private static readonly string[] DeclareWayHints =
            ["sbfs", "declare_way", "report_way", "deduction_way", "申报方式", "扣除方式"];

        private static readonly string[] VerifyStateHints =
            ["yzqk", "verify_state", "verification_state", "verify_state_name", "验证状态", "验证情况"];

        private static readonly string[] ReportStateHints =
            ["report_state", "reportstate", "download_state", "报送状态", "下载状态", "状态"];

        /// <summary>扣除项目编码 → 名称（个税专项附加扣除的通用码表；未识别时保留原值）。</summary>
        private static readonly Dictionary<string, string> ProjectNames = new(StringComparer.Ordinal)
        {
            ["1"] = "子女教育",
            ["2"] = "继续教育",
            ["3"] = "大病医疗",
            ["4"] = "住房贷款利息",
            ["5"] = "住房租金",
            ["6"] = "赡养老人",
            ["7"] = "3岁以下婴幼儿照护",
        };

        /// <summary>申报方式编码 → 名称。</summary>
        private static readonly Dictionary<string, string> DeclareWayNames = new(StringComparer.Ordinal)
        {
            ["1"] = "通过扣缴义务人申报",
            ["2"] = "综合所得年度自行申报",
        };

        public static IReadOnlyList<TaxIitDeductionRowDto> Parse(
            IReadOnlyList<CapturedJsonPayload> payloads,
            out IReadOnlyList<string> diagnostics)
        {
            var rows = new List<TaxIitDeductionRowDto>();
            var notes = new List<string>();

            foreach (var payload in payloads)
            {
                var sample = string.Empty;
                foreach (var array in IitPayloadReader.ArraysOf(payload.Body))
                {
                    var parsed = ParseArray(array);
                    if (parsed.Count > 0)
                    {
                        rows.AddRange(parsed);
                    }
                    else if (sample.Length == 0 && array[0] is JsonObject first)
                    {
                        sample = string.Join("|", first.Select(pair => pair.Key).Take(14));
                    }
                }

                if (sample.Length > 0)
                {
                    notes.Add($"{IitPayloadReader.Shorten(payload.Url)} 字段={sample}");
                }
            }

            var deduped = IitPayloadReader.DedupBy(rows, row => row.Key);
            diagnostics = deduped.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return deduped;
        }

        private static List<TaxIitDeductionRowDto> ParseArray(JsonArray array)
        {
            var rows = new List<TaxIitDeductionRowDto>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var project = MapProject(IitPayloadReader.PickText(obj, ProjectHints));
                var name = IitPayloadReader.PickText(obj, NameHints);
                var idNo = IitPayloadReader.PickText(obj, IdNoHints);
                if (project.Length == 0 || (name.Length == 0 && idNo.Length < 6))
                {
                    continue;
                }

                var amount = IitPayloadReader.PickDecimal(obj, AmountHints);
                rows.Add(new TaxIitDeductionRowDto
                {
                    // 同一人同一项目可能有多段有效期，把有效期与金额一并纳入去重键。
                    Key = $"{name}|{idNo}|{project}|{IitPayloadReader.PickDate(obj, PeriodStartHints)}|{amount}",
                    Name = name,
                    IdNo = idNo,
                    IdTypeText = IitPayloadReader.MapIdType(IitPayloadReader.PickText(obj, IdTypeHints)),
                    ProjectText = project,
                    RelativeText = IitPayloadReader.PickText(obj, RelativeHints),
                    YearText = IitPayloadReader.PickText(obj, YearHints),
                    PeriodStart = IitPayloadReader.PickDate(obj, PeriodStartHints),
                    PeriodEnd = IitPayloadReader.PickDate(obj, PeriodEndHints),
                    MonthlyAmount = amount,
                    DeclareWayText = MapDeclareWay(IitPayloadReader.PickText(obj, DeclareWayHints)),
                    VerifyStateText = IitPayloadReader.PickText(obj, VerifyStateHints),
                    ReportStateText = IitPayloadReader.PickText(obj, ReportStateHints),
                    Raw = obj.ToJsonString(IitPayloadReader.RawJsonOptions),
                });
                if (rows.Count >= MaxRows)
                {
                    break;
                }
            }

            return rows;
        }

        /// <summary>
        /// 项目字段可能直接给中文名，也可能给编码：纯数字按码表翻译，码表外的编码丢掉
        /// （认不出项目的行没有意义）；含中文的原样展示。
        /// </summary>
        private static string MapProject(string raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            if (ProjectNames.TryGetValue(raw, out var name))
            {
                return name;
            }

            return raw.Any(c => !char.IsAsciiDigit(c)) ? raw : string.Empty;
        }

        private static string MapDeclareWay(string raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            return DeclareWayNames.TryGetValue(raw, out var name) ? name : raw;
        }
    }
}
