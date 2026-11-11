using KuaiJi.SharedKernel.Infrastructure.Pdf;

namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>
    /// 账页打印 PDF 的表格投影：列、分组表头、缩进与合计行按各页屏幕表格 1:1 描述，
    /// 版式（A4、边框、跨页续表、页脚页码）由 <see cref="PrintSheetPdfBuilder"/> 统一负责。
    /// 与 <see cref="LedgerExportProjector"/>（压平成单行表头写 xlsx）取数口径相同，只是保留表头层级。
    /// </summary>
    public static class LedgerPdfProjector
    {
        /// <summary>各类账页 DTO 的公共页头，供控制器生成文件名与期间后缀。</summary>
        public static LedgerMetaDto MetaOf(object page) => page switch
        {
            SubsidiaryLedgerDto dto => dto.Meta,
            GeneralLedgerDto dto => dto.Meta,
            VoucherJournalDto dto => dto.Meta,
            VoucherSummaryDto dto => dto.Meta,
            AssistBalanceDto dto => dto.Meta,
            AssistSubsidiaryDto dto => dto.Meta,
            AssistCombineDto dto => dto.Meta,
            MultiColumnDto dto => dto.Meta,
            AgeingAnalysisDto dto => dto.Meta,
            _ => throw new LedgerException("未知的账页类型。"),
        };

        /// <summary>总账「本年累计」行需要的年窗口发生额（按科目编码），由调用方按 1 月至截止月另查一次。</summary>
        public sealed record GeneralYearTotals(IReadOnlyDictionary<string, (decimal Debit, decimal Credit)> ByAccountCode);

        public static PrintSheet Project(LedgerExportKind kind, object page, LedgerQueryOptions? options = null, GeneralYearTotals? yearTotals = null) => kind switch
        {
            LedgerExportKind.Subsidiary => Subsidiary((SubsidiaryLedgerDto)page, options),
            LedgerExportKind.General => General((GeneralLedgerDto)page, options, yearTotals),
            LedgerExportKind.VoucherSummary => VoucherSummary((VoucherSummaryDto)page, options),
            LedgerExportKind.MultiColumn => MultiColumn((MultiColumnDto)page),
            LedgerExportKind.AssistBalance => AssistBalance((AssistBalanceDto)page),
            LedgerExportKind.AssistSubsidiary => AssistSubsidiary((AssistSubsidiaryDto)page, options),
            _ => throw new LedgerException("该账页暂不支持打印。"),
        };

        /// <summary>明细账：科目列按行重复编码与名称，末尾一张合计行（只累加发生额，与页面合计口径一致）。</summary>
        private static PrintSheet Subsidiary(SubsidiaryLedgerDto dto, LedgerQueryOptions? options)
        {
            var showCounterAccount = options?.ShowCounterAccount ?? true;
            var columns = new List<PrintColumn>
            {
                new("日期", Width: "11%", Align: PdfAlign.Center),
                new("凭证字号", Width: "10%", Align: PdfAlign.Center),
                new("科目", Width: "20%"),
                new("摘要"),
            };
            if (showCounterAccount) columns.Add(new PrintColumn("对方科目", Width: "14%"));
            columns.Add(new PrintColumn("借方", Width: "11%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("贷方", Width: "11%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("方向", Width: "6%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn("余额", Width: "11%", Align: PdfAlign.Right));

            var rows = new List<PrintRow>();
            decimal totalDebit = 0m;
            decimal totalCredit = 0m;
            foreach (var account in dto.Accounts)
            {
                var label = $"{account.Code} {account.Name}";
                foreach (var row in account.Rows)
                {
                    if (row.Kind == SubsidiaryRowKind.Entry)
                    {
                        totalDebit += row.Debit;
                        totalCredit += row.Credit;
                    }

                    rows.Add(new PrintRow(CellList(row.Date, row.VoucherNo, label, row.Summary,
                        showCounterAccount ? row.CounterAccount : null,
                        row.Debit, row.Credit, row.Direction, row.Balance)));
                }
            }

            // 合计行的借贷金额排在方向、余额之前，与页面表尾一致。
            var span = showCounterAccount ? 5 : 4;
            rows.Add(new PrintRow(
            [
                new PrintCell("合计", ColSpan: span, Css: PdfCss.Strong),
                new PrintCell(totalDebit, Align: PdfAlign.Right, Css: PdfCss.Strong),
                new PrintCell(totalCredit, Align: PdfAlign.Right, Css: PdfCss.Strong),
                new PrintCell(null, ColSpan: 2),
            ]));

            var single = dto.Accounts.Count == 1 ? dto.Accounts[0] : null;
            return Sheet(
                single is null ? "明细账" : $"{single.Name} 明细账",
                dto.Meta,
                columns,
                rows,
                subject: SubjectOf(options, dto.Accounts.Select(a => (a.Code, a.Name))));
        }

        /// <summary>总账：每科目一段（科目名称行 + 期初余额 / 本期合计 / 本年累计三行），与页面行序一致。</summary>
        private static PrintSheet General(GeneralLedgerDto dto, LedgerQueryOptions? options, GeneralYearTotals? yearTotals)
        {
            var columns = new List<PrintColumn>
            {
                new("科目编码", Width: "12%", Align: PdfAlign.Center),
                new("科目名称", Width: "18%"),
                new("期间", Width: "10%", Align: PdfAlign.Center),
                new("摘要", Width: "18%"),
                new("借方金额", Width: "13%", Align: PdfAlign.Right),
                new("贷方金额", Width: "13%", Align: PdfAlign.Right),
                new("方向", Width: "6%", Align: PdfAlign.Center),
                new("余额", Width: "13%", Align: PdfAlign.Right),
            };

            var rows = new List<PrintRow>();
            foreach (var account in dto.Accounts)
            {
                var lines = account.Rows ?? [];
                rows.Add(new PrintRow([new PrintCell(account.Name, ColSpan: columns.Count, Css: PdfCss.Strong)]));
                if (lines.Count == 0) continue;

                var first = lines[0];
                var last = lines[^1];
                var endingBalance = last.EndingDebit != 0m ? last.EndingDebit : last.EndingCredit;
                var period = PeriodOf(last.PeriodText);

                rows.Add(new PrintRow(CellList(account.Code, account.Name, period, "期初余额",
                    first.OpeningDebit, first.OpeningCredit,
                    first.OpeningDebit > 0m ? "借" : first.OpeningCredit > 0m ? "贷" : "平",
                    first.OpeningDebit != 0m ? first.OpeningDebit : first.OpeningCredit)));

                // 区间内无发生额时不出合计行，与页面「隐藏无发生额」口径一致。
                if (account.TotalDebit == 0m && account.TotalCredit == 0m && options?.HideTotalsNoActivity == true) continue;

                rows.Add(new PrintRow(CellList(account.Code, account.Name, period, "本期合计",
                    account.TotalDebit, account.TotalCredit, last.Direction, endingBalance), PdfCss.Strong));

                var year = yearTotals?.ByAccountCode.GetValueOrDefault(account.Code);
                rows.Add(new PrintRow(CellList(account.Code, account.Name, period, "本年累计",
                    year?.Debit, year?.Credit, last.Direction, endingBalance), PdfCss.Strong));
            }

            return Sheet("总账", dto.Meta, columns, rows,
                subject: SubjectOf(options, dto.Accounts.Select(a => (a.Code, a.Name))));
        }

        /// <summary>科目汇总表：科目编码/名称 + 「金额合计」分组下的借方、贷方，末行合计。</summary>
        private static PrintSheet VoucherSummary(VoucherSummaryDto dto, LedgerQueryOptions? options)
        {
            var columns = new List<PrintColumn>
            {
                new("科目编码", Width: "18%", Align: PdfAlign.Center),
                new("科目名称", Width: "32%"),
                new("借方", "金额合计", "25%", PdfAlign.Right),
                new("贷方", "金额合计", "25%", PdfAlign.Right),
            };

            // 页面把平表挂成树（缩进 18px/级），打印用名称列缩进还原层级。
            var byCode = dto.Rows.ToDictionary(row => row.Code);
            var rows = dto.Rows
                .Select(row => new PrintRow(
                [
                    new PrintCell(row.Code, Align: PdfAlign.Center),
                    new PrintCell(row.Name, Css: IndentOf(row, byCode)),
                    new PrintCell(row.Debit, Align: PdfAlign.Right),
                    new PrintCell(row.Credit, Align: PdfAlign.Right),
                ]))
                .ToList();
            rows.Add(new PrintRow(
            [
                PrintCell.Blank,
                new("合计", Css: PdfCss.Strong),
                new(PdfText.AmountAlways(dto.TotalDebit), Align: PdfAlign.Right, Css: PdfCss.Strong),
                new(PdfText.AmountAlways(dto.TotalCredit), Align: PdfAlign.Right, Css: PdfCss.Strong),
            ]));

            return Sheet("凭证汇总表", dto.Meta, columns, rows,
                subject: SubjectOf(options, dto.Rows.Select(row => (row.Code, row.Name))),
                notes: [$"【凭证数：{dto.VoucherCount}张　附件数：{dto.AttachmentCount}张】"]);
        }

        /// <summary>多栏账：日期/凭证字号/摘要 + 每栏科目一个分组（借方、贷方）+ 方向、余额，末行各栏逐笔合计。</summary>
        private static PrintSheet MultiColumn(MultiColumnDto dto)
        {
            var columns = new List<PrintColumn>
            {
                new("日期", Width: "10%", Align: PdfAlign.Center),
                new("凭证字号", Width: "9%", Align: PdfAlign.Center),
                new("摘要", Width: "18%"),
            };

            foreach (var header in dto.Columns)
            {
                var group = $"{header.Code} {header.Name}";
                columns.Add(new PrintColumn("借方", group, "8%", PdfAlign.Right));
                columns.Add(new PrintColumn("贷方", group, "8%", PdfAlign.Right));
            }

            columns.Add(new PrintColumn("方向", Width: "6%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn("余额", Width: "11%", Align: PdfAlign.Right));

            var debitTotals = new decimal[dto.Columns.Count];
            var creditTotals = new decimal[dto.Columns.Count];
            var rows = new List<PrintRow>();
            foreach (var row in dto.Rows)
            {
                var cells = new List<PrintCell> { new(row.Date), new(row.VoucherNo, Align: PdfAlign.Center), new(row.Summary) };
                for (var i = 0; i < dto.Columns.Count; i++)
                {
                    var debit = i < row.Debits.Count ? row.Debits[i] : 0m;
                    var credit = i < row.Credits.Count ? row.Credits[i] : 0m;
                    if (row.Kind == SubsidiaryRowKind.Entry)
                    {
                        debitTotals[i] += debit;
                        creditTotals[i] += credit;
                    }

                    cells.Add(new PrintCell(debit, Align: PdfAlign.Right));
                    cells.Add(new PrintCell(credit, Align: PdfAlign.Right));
                }

                cells.Add(new PrintCell(row.Direction, Align: PdfAlign.Center));
                cells.Add(new PrintCell(row.Balance, Align: PdfAlign.Right));
                rows.Add(new PrintRow(cells, row.Kind == SubsidiaryRowKind.Entry ? "" : PdfCss.Strong));
            }

            var total = new List<PrintCell>
            {
                new(null, ColSpan: 2),
                new("本区间逐笔行各栏合计", ColSpan: columns.Count - 2 - 2 * dto.Columns.Count - 2, Css: PdfCss.Strong),
            };
            for (var i = 0; i < dto.Columns.Count; i++)
            {
                total.Add(new PrintCell(debitTotals[i], Align: PdfAlign.Right));
                total.Add(new PrintCell(creditTotals[i], Align: PdfAlign.Right));
            }

            total.Add(new PrintCell(null, ColSpan: 2));
            rows.Add(new PrintRow(total, PdfCss.Strong));

            return Sheet("多栏账", dto.Meta, columns, rows, landscape: true,
                subject: string.IsNullOrWhiteSpace(dto.Path) ? null : dto.Path);
        }

        /// <summary>核算项目余额表：维度编码/名称 + 期初余额 / 本期发生额 / 期末余额三组借贷列。</summary>
        private static PrintSheet AssistBalance(AssistBalanceDto dto)
        {
            var columns = BalanceColumns(dto.AssistTypeText);
            var rows = new List<PrintRow>();
            foreach (var account in dto.Accounts)
            {
                // 页面上每个科目是一段小标题，表格本身不含科目列。
                rows.Add(new PrintRow([new PrintCell($"{account.Code} {account.Name}", ColSpan: columns.Count, Css: PdfCss.Strong)]));
                foreach (var row in account.Rows)
                {
                    rows.Add(new PrintRow(CellList(row.Code, row.Name,
                        row.OpeningDebit, row.OpeningCredit, row.PeriodDebit, row.PeriodCredit, row.EndingDebit, row.EndingCredit)));
                }
            }

            return Sheet("核算项目余额表", dto.Meta, columns, rows,
                subject: dto.AssistTypeText, subjectLabel: "类别");
        }

        /// <summary>核算项目明细账：日期/凭证字号/科目(含档案)/摘要/[对方科目]/借方/贷方/方向/余额，每档案一段合计。</summary>
        private static PrintSheet AssistSubsidiary(AssistSubsidiaryDto dto, LedgerQueryOptions? options)
        {
            var showCounterAccount = options?.ShowCounterAccount ?? true;
            var columns = new List<PrintColumn>
            {
                new("日期", Width: "10%", Align: PdfAlign.Center),
                new("凭证字号", Width: "9%", Align: PdfAlign.Center),
                new("科目", Width: "22%"),
                new("摘要"),
            };
            if (showCounterAccount) columns.Add(new PrintColumn("对方科目", Width: "13%"));
            columns.Add(new PrintColumn("借方", Width: "10%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("贷方", Width: "10%", Align: PdfAlign.Right));
            columns.Add(new PrintColumn("方向", Width: "6%", Align: PdfAlign.Center));
            columns.Add(new PrintColumn("余额", Width: "10%", Align: PdfAlign.Right));

            var rows = new List<PrintRow>();
            foreach (var account in dto.Accounts)
            {
                var accountLabel = $"{account.Code} {account.Name}";
                foreach (var item in account.Items)
                {
                    // 页面上档案是一段小标题（带借贷合计），打印折成一整行的分组头。
                    rows.Add(new PrintRow([new PrintCell(
                        $"{item.Code} {item.Name}　　借方 {PdfText.AmountAlways(item.TotalDebit)} ／ 贷方 {PdfText.AmountAlways(item.TotalCredit)}",
                        ColSpan: columns.Count, Css: PdfCss.Strong)]));
                    foreach (var row in item.Rows)
                    {
                        rows.Add(new PrintRow(CellList(row.Date, row.VoucherNo, accountLabel, row.Summary,
                            showCounterAccount ? row.CounterAccount : null,
                            row.Debit, row.Credit, row.Direction, row.Balance)));
                    }
                }
            }

            return Sheet("核算项目明细账", dto.Meta, columns, rows,
                subject: dto.AssistTypeText, subjectLabel: "类别");
        }

        private static List<PrintColumn> BalanceColumns(string assistTypeText) =>
        [
            new($"{assistTypeText}编码", null, "12%", PdfAlign.Center),
            new($"{assistTypeText}名称", null, "20%"),
            new("借方", "期初余额", "11%", PdfAlign.Right),
            new("贷方", "期初余额", "11%", PdfAlign.Right),
            new("借方", "本期发生额", "11%", PdfAlign.Right),
            new("贷方", "本期发生额", "11%", PdfAlign.Right),
            new("借方", "期末余额", "12%", PdfAlign.Right),
            new("贷方", "期末余额", "12%", PdfAlign.Right),
        ];

        private static PrintSheet Sheet(
            string title,
            LedgerMetaDto meta,
            IReadOnlyList<PrintColumn> columns,
            IReadOnlyList<PrintRow> rows,
            string? subject = null,
            string subjectLabel = "科目",
            bool landscape = false,
            IReadOnlyList<string>? notes = null)
        {
            var head = new List<PrintMeta>
            {
                new($"编制单位：{meta.CompanyName}"),
                new($"账套：{meta.AccountSetName}", PdfAlign.Center),
            };
            if (!string.IsNullOrWhiteSpace(subject)) head.Add(new PrintMeta($"{subjectLabel}：{subject}", PdfAlign.Center));
            head.Add(new PrintMeta($"期间：{meta.PeriodText}", PdfAlign.Right));

            var tail = new List<string> { "单位：元" };
            if (meta.UnpostedVoucherCount > 0)
            {
                tail.Add($"本期有 {meta.UnpostedVoucherCount} 张未过账凭证，未纳入账簿。");
            }

            if (notes is { Count: > 0 }) tail.AddRange(notes);

            return new PrintSheet
            {
                Title = title,
                Meta = head,
                Columns = columns,
                Rows = rows,
                Notes = tail,
                Landscape = landscape,
                PrintedAt = DateTimeOffset.Now,
                EmptyText = "该期间暂无数据，请确认凭证已过账。",
            };
        }

        /// <summary>页头「科目」：单科目、编码区间或「所有」，与 <see cref="LedgerExportProjector"/> 同口径。</summary>
        private static string SubjectOf(LedgerQueryOptions? options, IEnumerable<(string Code, string Name)> accounts)
        {
            var from = options?.AccountFrom;
            var to = options?.AccountTo;
            if (string.IsNullOrEmpty(from) && string.IsNullOrEmpty(to)) return "所有";
            if (!string.IsNullOrEmpty(from) && from == to)
            {
                var name = accounts.FirstOrDefault(a => a.Code == from).Name;
                return string.IsNullOrWhiteSpace(name) ? from : $"{from} {name}";
            }

            return $"{from} 至 {to}";
        }

        /// <summary>科目汇总表行的缩进：沿 ParentCode 上溯到根的层数。</summary>
        private static string IndentOf(VoucherSummaryRowDto row, IReadOnlyDictionary<string, VoucherSummaryRowDto> byCode)
        {
            var level = 0;
            for (var parent = row.ParentCode; parent is not null && byCode.TryGetValue(parent, out var upstream) && level < 9; parent = upstream.ParentCode)
            {
                level++;
            }

            return level switch { 1 => PdfCss.Indent1, > 1 => PdfCss.Indent2, _ => "" };
        }

        /// <summary>期间标识 2026-07 → 「2026年7月」，与页面期间列一致。</summary>
        private static string PeriodOf(string? periodText)
        {
            if (string.IsNullOrWhiteSpace(periodText)) return string.Empty;
            var parts = periodText.Split('-', ':');
            return parts.Length >= 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month)
                ? $"{year}年{month}月"
                : periodText;
        }

        /// <summary>把一行值转成单元格：文本与金额按 <see cref="PrintSheetPdfBuilder"/> 的规则各自格式化。</summary>
        private static IReadOnlyList<PrintCell> CellList(params object?[] values) =>
            values.Select(value => new PrintCell(value)).ToList();
    }
}
