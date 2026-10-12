namespace KuaiJi.Modules.Ledgers.Application
{
    /// <summary>
    /// 一张待导出的账页：压平后的列名 + 表体行 + 页头补充说明。
    /// 多级表头在此压平成「组名-子列名」，由基础设施层用 MiniExcel 写成 .xlsx。
    /// </summary>
    public sealed class LedgerExportSheet
    {
        /// <summary>工作表名（账页中文名）。</summary>
        public required string Name { get; init; }

        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>压平后的列名，顺序与 <see cref="Rows"/> 每行的单元格一致。</summary>
        public required IReadOnlyList<string> Columns { get; init; }

        /// <summary>表体行，含各段合计行与最后的总计行。</summary>
        public required IReadOnlyList<IReadOnlyList<object?>> Rows { get; init; }

        /// <summary>账页特有信息（如多栏账的科目路径、账龄的口径说明），追加在页头工作表。</summary>
        public IReadOnlyList<KeyValuePair<string, string>> ExtraInfo { get; init; } = [];
    }

    /// <summary>把各账页的查询结果投影成 <see cref="LedgerExportSheet"/>，列与页面表格一致。</summary>
    public static class LedgerExportProjector
    {
        /// <summary>按账页种类投影导出内容；<paramref name="page"/> 须是对应种类的 DTO。</summary>
        public static LedgerExportSheet Project(LedgerExportKind kind, object page) => kind switch
        {
            LedgerExportKind.Subsidiary => Subsidiary((SubsidiaryLedgerDto)page),
            LedgerExportKind.General => General((GeneralLedgerDto)page),
            LedgerExportKind.Journal => Journal((VoucherJournalDto)page),
            LedgerExportKind.VoucherSummary => VoucherSummary((VoucherSummaryDto)page),
            LedgerExportKind.AssistBalance => AssistBalance((AssistBalanceDto)page),
            LedgerExportKind.AssistSubsidiary => AssistSubsidiary((AssistSubsidiaryDto)page),
            LedgerExportKind.AssistCombine => AssistCombine((AssistCombineDto)page),
            LedgerExportKind.MultiColumn => MultiColumn((MultiColumnDto)page),
            LedgerExportKind.Ageing => Ageing((AgeingAnalysisDto)page),
            _ => throw new LedgerException("不支持导出的账页类型。"),
        };

        private static LedgerExportSheet Subsidiary(SubsidiaryLedgerDto dto)
        {
            var columns = new[] { "科目编码", "科目名称", "日期", "凭证号", "摘要", "对方科目", "借方", "贷方", "方向", "余额" };
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var account in dto.Accounts)
            {
                foreach (var row in account.Rows)
                {
                    rows.Add(new object?[]
                    {
                        account.Code, account.Name, row.Date, row.VoucherNo, row.Summary,
                        row.CounterAccount, row.Debit, row.Credit, row.Direction, row.Balance,
                    });
                }

                rows.Add(new object?[]
                {
                    account.Code, account.Name, null, null, "合计", null, account.TotalDebit, account.TotalCredit, null, null,
                });
            }

            return Sheet(LedgerExportKind.Subsidiary, dto.Meta, columns, rows);
        }

        private static LedgerExportSheet General(GeneralLedgerDto dto)
        {
            var columns = new[]
            {
                "科目编码", "科目名称", "期间",
                "期初余额-借方", "期初余额-贷方", "本期发生-借方", "本期发生-贷方", "期末余额-借方", "期末余额-贷方", "方向",
            };
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var account in dto.Accounts)
            {
                foreach (var row in account.Rows)
                {
                    rows.Add(new object?[]
                    {
                        account.Code, account.Name, row.PeriodText,
                        row.OpeningDebit, row.OpeningCredit, row.Debit, row.Credit, row.EndingDebit, row.EndingCredit, row.Direction,
                    });
                }

                rows.Add(new object?[]
                {
                    account.Code, account.Name, "合计", null, null, account.TotalDebit, account.TotalCredit, null, null, null,
                });
            }

            return Sheet(LedgerExportKind.General, dto.Meta, columns, rows);
        }

        private static LedgerExportSheet Journal(VoucherJournalDto dto)
        {
            var columns = new[] { "日期", "凭证号", "摘要", "科目编码", "科目名称", "借方", "贷方", "制单人", "审核人", "附件张数" };
            var rows = dto.Rows
                .Select(r => (IReadOnlyList<object?>)new object?[]
                {
                    r.Date, r.VoucherNo, r.Summary, r.AccountCode, r.AccountName, r.Debit, r.Credit, r.Preparer, r.Reviewer, r.AttachmentCount,
                })
                .ToList();

            rows.Add(new object?[]
            {
                null, null, $"合计（{dto.VoucherCount} 张）", null, null, dto.TotalDebit, dto.TotalCredit, null, null, dto.TotalAttachments,
            });

            return Sheet(LedgerExportKind.Journal, dto.Meta, columns, rows);
        }

        private static LedgerExportSheet VoucherSummary(VoucherSummaryDto dto)
        {
            var columns = new[] { "科目编码", "科目名称", "凭证张数", "借方", "贷方" };
            var rows = dto.Rows
                .Select(r => (IReadOnlyList<object?>)new object?[] { r.Code, r.Name, r.VoucherCount, r.Debit, r.Credit })
                .ToList();

            rows.Add(new object?[] { "合计", null, dto.VoucherCount, dto.TotalDebit, dto.TotalCredit });

            return Sheet(LedgerExportKind.VoucherSummary, dto.Meta, columns, rows,
                new KeyValuePair<string, string>[] { new("附件张数", dto.AttachmentCount.ToString()) });
        }

        private static LedgerExportSheet AssistBalance(AssistBalanceDto dto)
        {
            var columns = new[]
            {
                "科目编码", "科目名称", $"{dto.AssistTypeText}编码", $"{dto.AssistTypeText}名称",
                "期初余额-借方", "期初余额-贷方", "本期发生-借方", "本期发生-贷方", "期末余额-借方", "期末余额-贷方", "方向",
            };
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var account in dto.Accounts)
            {
                foreach (var row in account.Rows)
                {
                    rows.Add(new object?[]
                    {
                        account.Code, account.Name, row.Code, row.Name,
                        row.OpeningDebit, row.OpeningCredit, row.PeriodDebit, row.PeriodCredit, row.EndingDebit, row.EndingCredit, row.Direction,
                    });
                }
            }

            return Sheet(LedgerExportKind.AssistBalance, dto.Meta, columns, rows);
        }

        private static LedgerExportSheet AssistSubsidiary(AssistSubsidiaryDto dto)
        {
            var columns = new[]
            {
                "科目编码", "科目名称", $"{dto.AssistTypeText}编码", $"{dto.AssistTypeText}名称",
                "日期", "凭证号", "摘要", "对方科目", "借方", "贷方", "方向", "余额",
            };
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var account in dto.Accounts)
            {
                foreach (var item in account.Items)
                {
                    foreach (var row in item.Rows)
                    {
                        rows.Add(new object?[]
                        {
                            account.Code, account.Name, item.Code, item.Name,
                            row.Date, row.VoucherNo, row.Summary, row.CounterAccount, row.Debit, row.Credit, row.Direction, row.Balance,
                        });
                    }

                    rows.Add(new object?[]
                    {
                        account.Code, account.Name, item.Code, item.Name, null, null, "合计", null, item.TotalDebit, item.TotalCredit, null, null,
                    });
                }
            }

            return Sheet(LedgerExportKind.AssistSubsidiary, dto.Meta, columns, rows);
        }

        private static LedgerExportSheet AssistCombine(AssistCombineDto dto)
        {
            // 次维度档案逐一对应「借方 / 贷方」两列，列名带编码避免重名档案互相覆盖。
            var columns = new List<string> { $"主{dto.AssistTypeText}编码", $"主{dto.AssistTypeText}名称" };
            foreach (var column in dto.Columns)
            {
                var title = $"{column.Code} {column.Name}";
                columns.Add($"{title}-借方");
                columns.Add($"{title}-贷方");
            }

            columns.Add("合计-借方");
            columns.Add("合计-贷方");

            var rows = new List<IReadOnlyList<object?>>();
            foreach (var group in dto.Groups)
            {
                var cells = new List<object?> { group.Code, group.Name };
                for (var i = 0; i < dto.Columns.Count; i++)
                {
                    cells.Add(i < group.Cells.Count ? group.Cells[i].Debit : 0m);
                    cells.Add(i < group.Cells.Count ? group.Cells[i].Credit : 0m);
                }

                cells.Add(group.TotalDebit);
                cells.Add(group.TotalCredit);
                rows.Add(cells);
            }

            var total = new List<object?> { "合计", null };
            total.AddRange(Enumerable.Repeat<object?>(0m, dto.Columns.Count * 2));
            total.Add(dto.TotalDebit);
            total.Add(dto.TotalCredit);
            rows.Add(total);

            return Sheet(LedgerExportKind.AssistCombine, dto.Meta, columns, rows,
                new KeyValuePair<string, string>[]
                {
                    new("主维度", dto.AssistTypeText),
                    new("次维度", dto.SecondaryAssistTypeText),
                });
        }

        private static LedgerExportSheet MultiColumn(MultiColumnDto dto)
        {
            var columns = new List<string> { "日期", "凭证号", "摘要" };
            foreach (var header in dto.Columns)
            {
                var title = $"{header.Code} {header.Name}";
                columns.Add($"{title}-借方");
                columns.Add($"{title}-贷方");
            }

            columns.Add("方向");
            columns.Add("余额");

            var debitTotals = new decimal[dto.Columns.Count];
            var creditTotals = new decimal[dto.Columns.Count];
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var row in dto.Rows)
            {
                var cells = new List<object?> { row.Date, row.VoucherNo, row.Summary };
                for (var i = 0; i < dto.Columns.Count; i++)
                {
                    var debit = i < row.Debits.Count ? row.Debits[i] : 0m;
                    var credit = i < row.Credits.Count ? row.Credits[i] : 0m;
                    // 「本月合计」「本年累计」行同样带各栏金额，累加时只取逐笔行避免重复。
                    if (row.Kind == SubsidiaryRowKind.Entry)
                    {
                        debitTotals[i] += debit;
                        creditTotals[i] += credit;
                    }

                    cells.Add(debit);
                    cells.Add(credit);
                }

                cells.Add(row.Direction);
                cells.Add(row.Balance);
                rows.Add(cells);
            }

            var total = new List<object?> { null, null, "本区间逐笔行各栏合计" };
            for (var i = 0; i < dto.Columns.Count; i++)
            {
                total.Add(debitTotals[i]);
                total.Add(creditTotals[i]);
            }

            total.Add(null);
            total.Add(null);
            rows.Add(total);

            return Sheet(LedgerExportKind.MultiColumn, dto.Meta, columns, rows,
                new KeyValuePair<string, string>[] { new("科目", dto.Path) });
        }

        private static LedgerExportSheet Ageing(AgeingAnalysisDto dto)
        {
            var columns = new List<string> { "科目编码", "科目名称", $"{dto.AssistTypeText}编码", $"{dto.AssistTypeText}名称", "方向", "期末余额" };
            columns.AddRange(dto.BucketLabels);

            var bucketTotals = new decimal[dto.BucketLabels.Count];
            var rows = new List<IReadOnlyList<object?>>();

            foreach (var account in dto.Accounts)
            {
                foreach (var item in account.Items)
                {
                    var cells = new List<object?> { account.Code, account.Name, item.Code, item.Name, item.Direction, item.Total };
                    for (var i = 0; i < dto.BucketLabels.Count; i++)
                    {
                        var amount = i < item.Buckets.Count ? item.Buckets[i].Amount : 0m;
                        bucketTotals[i] += amount;
                        cells.Add(amount);
                    }

                    rows.Add(cells);
                }
            }

            var total = new List<object?> { "合计", null, null, null, null, dto.Total };
            for (var i = 0; i < bucketTotals.Length; i++)
            {
                total.Add(bucketTotals[i]);
            }

            rows.Add(total);

            return Sheet(LedgerExportKind.Ageing, dto.Meta, columns, rows,
                new KeyValuePair<string, string>[]
                {
                    new("截止日期", dto.AsOfDate),
                    new("核算维度", dto.AssistTypeText),
                    new("口径说明", dto.Note),
                });
        }

        private static LedgerExportSheet Sheet(
            LedgerExportKind kind,
            LedgerMetaDto meta,
            IReadOnlyList<string> columns,
            IReadOnlyList<IReadOnlyList<object?>> rows,
            IReadOnlyList<KeyValuePair<string, string>>? extraInfo = null)
        {
            return new LedgerExportSheet
            {
                Name = LedgerExportKindText.Of(kind),
                Meta = meta,
                Columns = columns,
                Rows = rows,
                ExtraInfo = extraInfo ?? [],
            };
        }
    }
}
