using System.Text.Json.Nodes;
using KuaiJi.Modules.Tax.Application;

namespace KuaiJi.Modules.Tax.Infrastructure
{
    /// <summary>
    /// 把个税（自然人电子税务局）人员信息采集相关 JSON 响应解析成人员行。
    ///
    /// 个税接口的字段名有拼音缩写（e_name/e_id/mobile_phone）与中文键两套风格，
    /// 且各省前置系统可能再包一层，所以沿用税费种解析的思路：按「字段名语义 + 取值特征」
    /// 做启发式映射，不绑定某个固定 DTO；解析不到时把候选数组的字段名回传补规则。
    /// 递归找数组与按候选名取值的部分放在 <see cref="IitPayloadReader"/>，与专项附加扣除解析共用。
    /// </summary>
    internal static class IitPersonnelPayloadParser
    {
        private const int MaxRows = 2000;

        private static readonly string[] NameHints =
            ["e_name", "xm", "xingming", "name", "cname", "employee_name", "person_name", "姓名"];

        private static readonly string[] IdNoHints =
            ["e_id", "sfz", "zjhm", "idcard", "id_card", "idno", "id_no", "cardid", "id_number", "证件号码", "证件号", "身份证号"];

        private static readonly string[] IdTypeHints =
            ["e_type", "zjlx", "id_type", "idtype", "证件类型"];

        private static readonly string[] PhoneHints =
            ["mobile_phone", "mobile", "sjhm", "shouji", "phone", "tel", "telephone", "手机", "手机号码", "联系电话"];

        private static readonly string[] GenderHints = ["gender", "xb", "sex", "性别"];

        private static readonly string[] EmployTypeHints =
            ["employment_type", "employ_type", "rzsy", "cytype", "employee_type", "任职受雇从业类型", "从业类型", "任职受雇类型"];

        private static readonly string[] StatusHints =
            ["person_status", "personstate", "ryzt", "status", "state", "人员状态", "状态"];

        private static readonly string[] HireDateHints =
            ["start_date", "hiredate", "hire_date", "rzrq", "rz_date", "employ_date", "任职受雇从业日期", "入职日期", "任职日期"];

        private static readonly string[] LeaveDateHints =
            ["end_date", "leavedate", "leave_date", "lzrq", "离职日期"];

        private static readonly string[] DeptHints =
            ["department_name", "department", "dept", "bm", "bumen", "部门", "部门名称"];

        private static readonly string[] CodeHints =
            ["e_code", "code", "sn", "employee_code", "person_no", "编号", "序号", "工号"];

        private static readonly string[] ReportStateHints =
            ["report_state_name", "reportstate_name", "reportstatename", "report_state", "reportstate", "报送状态"];

        private static readonly string[] VerifyStateHints =
            ["verification_state_name", "verificationstate_name", "verify_state_name", "verification_state", "verificationstate", "验证状态", "身份验证状态"];

        private static readonly string[] DeductionHints =
            ["is_deduction_applied", "deduction_applied", "has_deduction", "是否采集专项附加扣除", "专项附加扣除"];

        /// <summary>任职受雇从业类型编码 → 名称。</summary>
        private static readonly Dictionary<string, string> EmployTypeNames = new(StringComparer.Ordinal)
        {
            ["1"] = "雇员",
            ["2"] = "保险营销员",
            ["3"] = "证券经纪人",
            ["4"] = "实习学生（全日制学历教育）",
            ["5"] = "其他",
        };

        public static IReadOnlyList<TaxIitPersonnelRowDto> Parse(
            IReadOnlyList<CapturedJsonPayload> payloads,
            out IReadOnlyList<string> diagnostics)
        {
            var rows = new List<TaxIitPersonnelRowDto>();
            var notes = new List<string>();

            foreach (var payload in payloads)
            {
                var sample = string.Empty;
                var arrays = IitPayloadReader.ArraysOf(payload.Body);
                if (arrays.Count == 0
                    && payload.Url.Contains("query", StringComparison.OrdinalIgnoreCase)
                    && payload.Url.Contains("dkdj", StringComparison.OrdinalIgnoreCase))
                {
                    // 查询类接口一个数组都没有：把顶层键回传，判断是加密报文还是这个单位真没人。
                    notes.Add($"{IitPayloadReader.Shorten(payload.Url)} 无数组");
                }

                foreach (var array in arrays)
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

            var deduped = DropMaskedTwins(IitPayloadReader.DedupBy(rows, row => row.Key));
            diagnostics = deduped.Count > 0 ? Array.Empty<string>() : notes.Take(20).ToList();
            return deduped;
        }

        /// <summary>
        /// 同一个名字既有脱敏行又有明文行时丢掉脱敏那份：点过明文开关后，页面上那份表格渲染明文、
        /// 瑞数密文之外的另一路报文还可能回脱敏号，两份都会进解析（Key 里号码不同，去重合并不了）。
        /// 留着脱敏行，同步时会先按姓名把档案认走、明文行再认领失败，一个人就被登记成两条档案。
        /// 同名两人都只拿到脱敏号时不动——那是真的分不出谁是谁，交给同步按姓名逐个认领。
        /// </summary>
        private static List<TaxIitPersonnelRowDto> DropMaskedTwins(List<TaxIitPersonnelRowDto> rows)
        {
            var real = rows.Where(row => !IsMasked(row.IdNo)).Select(row => row.Name).ToHashSet(StringComparer.Ordinal);
            return rows.Where(row => !IsMasked(row.IdNo) || !real.Contains(row.Name)).ToList();
        }

        /// <summary>脱敏判断：税局把中间几位换成星号（3412************97）。</summary>
        private static bool IsMasked(string idNo) => idNo.Contains('*', StringComparison.Ordinal);

        private static List<TaxIitPersonnelRowDto> ParseArray(JsonArray array)
        {
            var rows = new List<TaxIitPersonnelRowDto>();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    continue;
                }

                var name = IitPayloadReader.PickText(obj, NameHints);
                var idNo = IitPayloadReader.PickText(obj, IdNoHints);
                // 名字 + 证件号码同时成立才算人员行；证件号码至少 6 位，排除把别的数组误判进来。
                if (string.IsNullOrWhiteSpace(name) || idNo.Length < 6)
                {
                    continue;
                }

                rows.Add(new TaxIitPersonnelRowDto
                {
                    Key = $"{name}|{idNo}",
                    Code = IitPayloadReader.PickText(obj, CodeHints),
                    Name = name,
                    IdNo = idNo,
                    IdTypeText = IitPayloadReader.MapIdType(IitPayloadReader.PickText(obj, IdTypeHints)),
                    Phone = IitPayloadReader.PickText(obj, PhoneHints),
                    GenderText = MapGender(IitPayloadReader.PickText(obj, GenderHints), idNo),
                    EmployTypeText = MapEmployType(IitPayloadReader.PickText(obj, EmployTypeHints)),
                    StatusText = MapStatus(IitPayloadReader.PickText(obj, StatusHints)),
                    HireDate = IitPayloadReader.PickDate(obj, HireDateHints),
                    LeaveDate = IitPayloadReader.PickDate(obj, LeaveDateHints),
                    Dept = IitPayloadReader.PickText(obj, DeptHints),
                    ReportStateText = IitPayloadReader.PickText(obj, ReportStateHints),
                    VerifyStateText = IitPayloadReader.PickText(obj, VerifyStateHints),
                    DeductionText = IitPayloadReader.PickBoolText(obj, DeductionHints),
                    Raw = obj.ToJsonString(IitPayloadReader.RawJsonOptions),
                });
                if (rows.Count >= MaxRows)
                {
                    break;
                }
            }

            return rows;
        }

        private static string MapGender(string raw, string idNo)
        {
            if (raw is "1" or "男" or "M" or "m")
            {
                return "男";
            }

            if (raw is "2" or "0" or "女" or "F" or "f")
            {
                return "女";
            }

            // 没有性别字段时按身份证第 17 位奇偶推断（仅居民身份证）。
            // 脱敏号（3412************97）也是 18 位，第 17 位照样能取到数字，
            // 照推就会把整个单位的人按掩码尾巴判成男/女，所以带星号的一律不猜。
            if (idNo.Length == 18 && !idNo.Contains('*', StringComparison.Ordinal) && char.IsDigit(idNo[16]))
            {
                return int.IsOddInteger(idNo[16] - '0') ? "男" : "女";
            }

            return string.Empty;
        }

        private static string MapEmployType(string raw)
        {
            if (raw.Length == 0)
            {
                return string.Empty;
            }

            return EmployTypeNames.TryGetValue(raw, out var name) ? name : raw;
        }

        private static string MapStatus(string raw)
        {
            return raw switch
            {
                "0" or "正常" => "正常",
                "1" or "2" or "3" or "非正常" => "非正常",
                _ => raw,
            };
        }

    }
}
