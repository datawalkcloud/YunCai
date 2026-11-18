using YunCai.Modules.Assist.Domain;

namespace YunCai.Modules.Ledgers.Application
{
    /// <summary>账龄分析的一个时间档。</summary>
    public sealed class AgeingBucketDto
    {
        /// <summary>档位文案，如「90-180 天」。</summary>
        public required string Label { get; init; }

        /// <summary>该档未核销余额（按余额方向取正值）。</summary>
        public decimal Amount { get; init; }

        /// <summary>占该档案未核销总额的比例（0-1，四位小数）。</summary>
        public decimal Ratio { get; init; }
    }

    /// <summary>账龄分析的一个档案行。</summary>
    public sealed class AgeingItemDto
    {
        /// <summary>档案编码。</summary>
        public required string Code { get; init; }

        /// <summary>档案名称。</summary>
        public required string Name { get; init; }

        /// <summary>余额方向：借 / 贷 / 平。</summary>
        public required string Direction { get; init; }

        /// <summary>未核销余额合计（绝对值）。</summary>
        public decimal Total { get; init; }

        /// <summary>七个固定档的金额与占比。</summary>
        public IReadOnlyList<AgeingBucketDto> Buckets { get; init; } = [];
    }

    /// <summary>账龄分析的一个科目段。</summary>
    public sealed class AgeingAccountDto
    {
        /// <summary>科目编码。</summary>
        public required string Code { get; init; }

        /// <summary>科目名称。</summary>
        public required string Name { get; init; }

        /// <summary>该科目下各档案的账龄行。</summary>
        public IReadOnlyList<AgeingItemDto> Items { get; init; } = [];
    }

    /// <summary>账龄分析表。</summary>
    public sealed class AgeingAnalysisDto
    {
        /// <summary>页头公共信息。</summary>
        public required LedgerMetaDto Meta { get; init; }

        /// <summary>辅助核算维度中文名。</summary>
        public required string AssistTypeText { get; init; }

        /// <summary>账龄截止日（取查询区间末日）。</summary>
        public required string AsOfDate { get; init; }

        /// <summary>固定档位文案，供表头与合计行使用。</summary>
        public IReadOnlyList<string> BucketLabels { get; init; } = [];

        /// <summary>口径说明，前端原样显示在页脚。</summary>
        public required string Note { get; init; }

        /// <summary>按科目分段的账龄行。</summary>
        public IReadOnlyList<AgeingAccountDto> Accounts { get; init; } = [];

        /// <summary>全部档案的未核销余额合计。</summary>
        public decimal Total { get; init; }
    }

    /// <summary>
    /// 账龄分析表应用服务：按「科目 × 档案」的期末未核销余额，从最晚的分录开始向前倒序消耗，
    /// 剩余的净额落在哪笔分录上就归入该笔的账龄档（余额推龄法）。
    /// <para>
    /// 一期不做单据核销，因此页脚必须显示 <see cref="AgeingAnalysisDto.Note"/> 说明估算口径；
    /// 二期引入核销表后只需替换本服务的分配算法，DTO 不变。
    /// 反向分录（如贷方发生额冲减借方余额）已在期末净额中轧差，但不参与档位归属，
    /// 故红字冲回较多时账龄会偏向保守（落在更晚的档位）。
    /// </para>
    /// </summary>
    public sealed class AgeingAnalysisService
    {
        private static readonly (string Label, int MaxDays)[] Buckets =
        [
            ("30 天以内", 30),
            ("31-60 天", 60),
            ("61-90 天", 90),
            ("91-180 天", 180),
            ("181-360 天", 360),
            ("361-1080 天", 1080),
            ("1080 天以上", int.MaxValue),
        ];

        private readonly LedgerDataSetFactory _factory;

        public AgeingAnalysisService(LedgerDataSetFactory factory)
        {
            _factory = factory;
        }

        /// <summary>生成账龄分析表。</summary>
        public async Task<AgeingAnalysisDto> GetAsync(Guid accountSetId, LedgerQueryOptions options, CancellationToken cancellationToken = default)
        {
            var type = options.AssistType ?? throw new LedgerException("账龄分析需要按辅助核算维度统计，请先选择类别。");

            // 账龄只看截至期末的余额构成，无需把区间扩展到年初。
            var dataSet = await _factory.LoadAsync(accountSetId, options, cancellationToken);
            var factsByAccount = dataSet.FactsByAccount;
            var accounts = new List<AgeingAccountDto>();
            var total = 0m;

            foreach (var account in dataSet.SelectAccounts())
            {
                var subtreeIds = dataSet.SubtreeAccountIds(account);
                var items = new List<AgeingItemDto>();

                // 一次遍历把该科目（含下级）的分录按档案归组，避免逐档案重复扫描。
                var factsByItem = new Dictionary<Guid, List<LedgerFact>>();
                foreach (var fact in subtreeIds.SelectMany(id => factsByAccount[id]))
                {
                    foreach (var assist in fact.Assists)
                    {
                        if (assist.Type != type)
                        {
                            continue;
                        }

                        if (!factsByItem.TryGetValue(assist.ItemId, out var list))
                        {
                            list = [];
                            factsByItem[assist.ItemId] = list;
                        }

                        list.Add(fact);
                    }
                }

                foreach (var itemId in dataSet.AssistItemIds(type, subtreeIds))
                {
                    var facts = factsByItem.GetValueOrDefault(itemId) ?? [];
                    var openingNet = subtreeIds.Sum(id => dataSet.OpeningByAccountAssist.GetValueOrDefault(new BalanceKey(id, type, itemId)));
                    var endingNet = openingNet + facts.Sum(f => f.NetDebit);
                    if (endingNet == 0m)
                    {
                        continue;
                    }

                    var amounts = AllocateBuckets(dataSet.EndDate, endingNet, facts);
                    var magnitude = Math.Abs(endingNet);
                    total += magnitude;

                    items.Add(new AgeingItemDto
                    {
                        Code = dataSet.AssistItemsById.GetValueOrDefault(itemId)?.Code ?? string.Empty,
                        Name = dataSet.AssistItemsById.GetValueOrDefault(itemId)?.Name ?? string.Empty,
                        Direction = LedgerDirectionText.Of(endingNet),
                        Total = magnitude,
                        Buckets = amounts
                            .Select((amount, index) => new AgeingBucketDto
                            {
                                Label = Buckets[index].Label,
                                Amount = amount,
                                Ratio = magnitude == 0m ? 0m : Math.Round(amount / magnitude, 4),
                            })
                            .ToList(),
                    });
                }

                if (items.Count > 0)
                {
                    accounts.Add(new AgeingAccountDto
                    {
                        Code = account.Code,
                        Name = account.Name,
                        Items = items.OrderBy(i => i.Code, StringComparer.Ordinal).ToList(),
                    });
                }
            }

            return new AgeingAnalysisDto
            {
                Meta = LedgerMetaDto.From(dataSet),
                AssistTypeText = AssistTypeText.Of(type),
                AsOfDate = dataSet.EndDate.ToString("yyyy-MM-dd"),
                BucketLabels = Buckets.Select(b => b.Label).ToList(),
                Note = "按先进后出法估算账龄（余额推龄），未做单据核销；期初结转余额计入最长档。",
                Accounts = accounts,
                Total = total,
            };
        }

        /// <summary>把期末净额按分录从晚到早倒序消耗，剩余的净额落在哪笔就归入哪笔的档位。</summary>
        private static decimal[] AllocateBuckets(DateOnly asOfDate, decimal endingNet, IReadOnlyList<LedgerFact> facts)
        {
            var amounts = new decimal[Buckets.Length];
            var sign = Math.Sign(endingNet);
            var remaining = Math.Abs(endingNet);

            foreach (var fact in facts.OrderByDescending(f => f.Date).ThenByDescending(f => f.Number).ThenByDescending(f => f.LineNumber))
            {
                if (remaining == 0m)
                {
                    break;
                }

                // 只消耗与期末余额同向的发生额，反向发生额已在净额中轧差。
                if (Math.Sign(fact.NetDebit) != sign)
                {
                    continue;
                }

                var taken = Math.Min(Math.Abs(fact.NetDebit), remaining);
                var days = asOfDate.DayNumber - fact.Date.DayNumber;
                amounts[BucketIndexOf(days)] += taken;
                remaining -= taken;
            }

            // 倒序消耗不完的余额早于区间起点，属于期初结转，计入最长档。
            if (remaining > 0m)
            {
                amounts[Buckets.Length - 1] += remaining;
            }

            return amounts;
        }

        private static int BucketIndexOf(int days)
        {
            for (var i = 0; i < Buckets.Length; i++)
            {
                if (days <= Buckets[i].MaxDays)
                {
                    return i;
                }
            }

            return Buckets.Length - 1;
        }
    }
}
