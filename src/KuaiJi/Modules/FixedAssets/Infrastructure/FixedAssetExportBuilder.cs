using System.Globalization;
using KuaiJi.Modules.FixedAssets.Application;
using MiniExcelLibs;

namespace KuaiJi.Modules.FixedAssets.Infrastructure
{
    /// <summary>
    /// 固定资产模块导出生成器（MiniExcel）：
    /// 资产卡片、资产导入模板、折旧摊销明细表、资产汇总表、资产类别、资产核对总账。
    /// 列名与导入解析一一对应，导出的资产表可直接改完再导回。
    /// </summary>
    public static class FixedAssetExportBuilder
    {
        /// <summary>.xlsx 对应的 MIME 类型。</summary>
        public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        /// <summary>资产卡片导出（列与导入模板一致，便于回填后原样导入）。</summary>
        public static byte[] BuildAssets(IReadOnlyList<FixedAssetDto> assets, IReadOnlyDictionary<string, string> categoryCodes)
        {
            var rows = assets.Select(a => new Dictionary<string, object?>
            {
                ["资产编号"] = a.Code,
                ["资产类别编码"] = categoryCodes.GetValueOrDefault(a.CategoryName) ?? string.Empty,
                ["资产类别"] = a.CategoryName,
                ["资产属性"] = a.AttrText,
                ["资产名称"] = a.Name,
                ["规格型号"] = a.Model ?? string.Empty,
                ["使用部门"] = a.Dept ?? string.Empty,
                ["开始使用日期"] = a.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["资产原值"] = a.OriginValue,
                ["累计折旧"] = a.AccumulatedDepreciation,
                ["资产净值"] = a.NetValue,
                ["状态"] = a.StatusText,
            }).ToList();

            if (rows.Count == 0)
            {
                rows.Add(EmptyAssetRow());
            }

            return Save(rows, "资产清单");
        }

        /// <summary>资产导入模板（仅表头 + 空行）。</summary>
        public static byte[] BuildAssetTemplate() => Save([EmptyAssetRow()], "资产清单");

        /// <summary>折旧摊销明细表。</summary>
        public static byte[] BuildDepreciationLedger(IReadOnlyList<DepreciationLedgerRowDto> rows, int period)
        {
            var periodText = PeriodText(period);
            var data = rows.Select(r => new Dictionary<string, object?>
            {
                ["会计期间"] = periodText,
                ["资产编号"] = r.AssetCode,
                ["资产类别"] = r.CategoryName,
                ["资产属性"] = r.AttrText,
                ["资产名称"] = r.AssetName,
                ["规格型号"] = r.Model ?? string.Empty,
                ["使用部门"] = r.Dept ?? string.Empty,
                ["资产原值"] = r.OriginValue,
                ["本期折旧"] = r.PeriodDepreciation,
                ["本年累计折旧"] = r.YearAccumulated,
                ["累计折旧"] = r.TotalAccumulated,
                ["减值准备"] = r.Impairment,
                ["资产净值"] = r.NetValue,
            }).ToList();

            data.Add(new Dictionary<string, object?>
            {
                ["会计期间"] = periodText,
                ["资产编号"] = "合计",
                ["资产类别"] = string.Empty,
                ["资产属性"] = string.Empty,
                ["资产名称"] = string.Empty,
                ["规格型号"] = string.Empty,
                ["使用部门"] = string.Empty,
                ["资产原值"] = rows.Sum(r => r.OriginValue),
                ["本期折旧"] = rows.Sum(r => r.PeriodDepreciation),
                ["本年累计折旧"] = rows.Sum(r => r.YearAccumulated),
                ["累计折旧"] = rows.Sum(r => r.TotalAccumulated),
                ["减值准备"] = 0m,
                ["资产净值"] = rows.Sum(r => r.NetValue),
            });

            return Save(data, "折旧摊销明细表");
        }

        /// <summary>资产汇总表（按部门或类别分组，末行合计）。</summary>
        public static byte[] BuildSummary(IReadOnlyList<FixedAssetSummaryRowDto> rows, bool byCategory)
        {
            var data = rows.Select(r => new Dictionary<string, object?>
            {
                ["项目"] = r.GroupName,
                ["原值-期初"] = r.OriginOpening,
                ["原值-本期增加"] = r.OriginIncrease,
                ["原值-本期减少"] = r.OriginDecrease,
                ["原值-期末"] = r.OriginClosing,
                ["累计折旧-期初"] = r.DepOpening,
                ["累计折旧-本期增加"] = r.DepIncrease,
                ["累计折旧-本期减少"] = r.DepDecrease,
                ["累计折旧-期末"] = r.DepClosing,
                ["减值准备-期初"] = r.ImpairOpening,
                ["减值准备-本期增加"] = r.ImpairIncrease,
                ["减值准备-本期减少"] = r.ImpairDecrease,
                ["减值准备-期末"] = r.ImpairClosing,
                ["净值-期初"] = r.NetOpening,
                ["净值-本期增加"] = r.NetIncrease,
                ["净值-本期减少"] = r.NetDecrease,
                ["净值-期末"] = r.NetClosing,
            }).ToList();

            data.Add(new Dictionary<string, object?>
            {
                ["项目"] = "合计",
                ["原值-期初"] = rows.Sum(r => r.OriginOpening),
                ["原值-本期增加"] = rows.Sum(r => r.OriginIncrease),
                ["原值-本期减少"] = rows.Sum(r => r.OriginDecrease),
                ["原值-期末"] = rows.Sum(r => r.OriginClosing),
                ["累计折旧-期初"] = rows.Sum(r => r.DepOpening),
                ["累计折旧-本期增加"] = rows.Sum(r => r.DepIncrease),
                ["累计折旧-本期减少"] = rows.Sum(r => r.DepDecrease),
                ["累计折旧-期末"] = rows.Sum(r => r.DepClosing),
                ["减值准备-期初"] = 0m,
                ["减值准备-本期增加"] = 0m,
                ["减值准备-本期减少"] = 0m,
                ["减值准备-期末"] = 0m,
                ["净值-期初"] = rows.Sum(r => r.NetOpening),
                ["净值-本期增加"] = rows.Sum(r => r.NetIncrease),
                ["净值-本期减少"] = rows.Sum(r => r.NetDecrease),
                ["净值-期末"] = rows.Sum(r => r.NetClosing),
            });

            return Save(data, byCategory ? "按资产类别汇总" : "按使用部门汇总");
        }

        /// <summary>资产类别导出。</summary>
        public static byte[] BuildCategories(IReadOnlyList<FixedAssetCategoryDto> categories)
        {
            var data = categories.Select(c => new Dictionary<string, object?>
            {
                ["类别编码"] = c.Code,
                ["类别名称"] = c.Name,
                ["折旧方法"] = c.MethodText,
                ["资产属性"] = c.AttrText,
                ["资产科目"] = string.IsNullOrEmpty(c.SubjectCode) ? string.Empty : $"{c.SubjectCode} {c.SubjectName}",
                ["使用月份"] = c.Months,
                ["预计净残值率"] = c.ResidualRate,
            }).ToList();

            if (data.Count == 0)
            {
                data.Add(new Dictionary<string, object?>
                {
                    ["类别编码"] = string.Empty,
                    ["类别名称"] = string.Empty,
                    ["折旧方法"] = string.Empty,
                    ["资产属性"] = string.Empty,
                    ["资产科目"] = string.Empty,
                    ["使用月份"] = string.Empty,
                    ["预计净残值率"] = string.Empty,
                });
            }

            return Save(data, "资产类别");
        }

        /// <summary>资产核对总账导出（类别分组行 + 会计科目 / 资产卡片 / 差异行）。</summary>
        public static byte[] BuildCheck(IReadOnlyList<FixedAssetCheckRowDto> rows)
        {
            var data = rows.Select(r => new Dictionary<string, object?>
            {
                ["项目"] = r.Item,
                ["名称"] = r.Name,
                ["期初余额"] = r.Opening,
                ["借方"] = r.Debit,
                ["贷方"] = r.Credit,
                ["余额"] = r.Balance,
            }).ToList();

            if (data.Count == 0)
            {
                data.Add(new Dictionary<string, object?>
                {
                    ["项目"] = string.Empty,
                    ["名称"] = string.Empty,
                    ["期初余额"] = string.Empty,
                    ["借方"] = string.Empty,
                    ["贷方"] = string.Empty,
                    ["余额"] = string.Empty,
                });
            }

            return Save(data, "资产核对总账");
        }

        /// <summary>期间 yyyyMM → yyyy-MM。</summary>
        public static string PeriodText(int period) => $"{period / 100:D4}-{period % 100:D2}";

        private static Dictionary<string, object?> EmptyAssetRow() => new()
        {
            ["资产编号"] = string.Empty,
            ["资产类别编码"] = string.Empty,
            ["资产类别"] = string.Empty,
            ["资产属性"] = string.Empty,
            ["资产名称"] = string.Empty,
            ["规格型号"] = string.Empty,
            ["使用部门"] = string.Empty,
            ["开始使用日期"] = string.Empty,
            ["资产原值"] = string.Empty,
            ["累计折旧"] = string.Empty,
            ["资产净值"] = string.Empty,
            ["状态"] = string.Empty,
        };

        private static byte[] Save(List<Dictionary<string, object?>> rows, string sheetName)
        {
            using var stream = new MemoryStream();
            MiniExcel.SaveAs(stream, new Dictionary<string, object> { [sheetName] = rows }, excelType: ExcelType.XLSX);
            return stream.ToArray();
        }
    }
}
