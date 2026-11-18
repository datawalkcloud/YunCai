using YunCai.Modules.Accounting.Domain;
using YunCai.Modules.Funds.Domain;

namespace YunCai.Modules.Funds.Application
{
    /// <summary>
    /// 建账预置（资金）：收支类别 19 条 + 默认现金账户 001 现金。
    /// 与新建账套时的预置保持一致，保证两边「同样输入」时起点相同。
    /// </summary>
    public static class FundPresetSeeder
    {
        /// <summary>默认收支类别：类型、名称、智能匹配关键字。</summary>
        private static readonly (EntryCategoryType Type, string Name, string Keywords)[] DefaultCategories =
        {
            (EntryCategoryType.Income, "销售收入", "销售,收入,回款,收款,货款,款项,项目款,零售,主营收入,往来款,入账,销货"),
            (EntryCategoryType.Income, "服务收入", "服务,劳务,计费,咨询,医疗,教育"),
            (EntryCategoryType.Income, "利息收入", "利息,结息,理财,入息,结算,结存款息"),
            (EntryCategoryType.Income, "股东投入", "投资,入资,股东,本金,注资,注册资本,注册资金,股金,股资"),
            (EntryCategoryType.Income, "短期借款", "借,借款,贷款,短期借款,周转,借入款,借用"),
            (EntryCategoryType.Income, "长期借款", "长期借款"),
            (EntryCategoryType.Income, "其他收入", "其他收入,备用,备用金,押金,租金"),
            (EntryCategoryType.Expense, "购买材料", "购买,采购,材料,原材料,支付,付款,货款,款项,料款,往来款,料费,购货,进货,购入"),
            (EntryCategoryType.Expense, "工资社保", "工资,薪金,社保,社会保险,薪酬,工薪,公积金,奖金,医疗,养老,年终,底薪"),
            (EntryCategoryType.Expense, "税费支出", "税,税费,增值税,所得税,缴纳,税款,税金,纳税,税务,交税,缴款"),
            (EntryCategoryType.Expense, "个人所得税", "个人,个税,个人所得"),
            (EntryCategoryType.Expense, "利息支出", "利息,利息支出,利息费用,付息"),
            (EntryCategoryType.Expense, "手续费", "手续,手续费,账户管理费,银行服务费"),
            (EntryCategoryType.Expense, "租金物业", "租金,物业,房租,租赁"),
            (EntryCategoryType.Expense, "水电费", "水电,水电费,水费,电费"),
            (EntryCategoryType.Expense, "运输费", "运输,运输费,运费,物流,快递费,货运,托运,空运,海运"),
            (EntryCategoryType.Expense, "差旅费", "差旅,差旅费,过路费,交通费,车费,出差,住宿,飞机票,火车票,高铁,往返,旅差费"),
            (EntryCategoryType.Expense, "招待费", "招待,招待费,餐费,餐饮,接待费,业务,礼品"),
            (EntryCategoryType.Expense, "其他支出", "其他支出,押金,保证金,垫付,垫支"),
        };

        /// <summary>预置收支类别与默认现金账户；已有数据时跳过。</summary>
        public static async Task SeedAsync(
            Guid accountSetId,
            IEntryCategoryRepository categoryRepository,
            IJournalRepository journalRepository,
            IAccountRepository accountRepository,
            CancellationToken cancellationToken = default)
        {
            // 1) 收支类别：收入 / 支出各一套，带智能匹配关键字
            var existingCategories = await categoryRepository.GetAllAsync(accountSetId, cancellationToken);
            if (existingCategories.Count == 0)
            {
                var code = 0;
                foreach (var (type, name, keywords) in DefaultCategories)
                {
                    code++;
                    var category = EntryCategory.Create(accountSetId, type, name, code.ToString());
                    category.SetSmartKeywords(keywords);
                    await categoryRepository.AddAsync(category, cancellationToken);
                }

                await categoryRepository.SaveChangesAsync(cancellationToken);
            }

            // 2) 默认现金账户 001 现金，挂「1001 库存现金」
            var journals = await journalRepository.GetAllAsync(accountSetId, cancellationToken);
            if (journals.Count == 0)
            {
                var accounts = await accountRepository.GetAllAsync(accountSetId, cancellationToken);
                var cash = accounts.FirstOrDefault(a => a.Code == "1001");
                if (cash is not null)
                {
                    var journal = Journal.Create(accountSetId, JournalType.Cash, "现金", cash.Id, 0m, "001");
                    await journalRepository.AddAsync(journal, cancellationToken);
                    await journalRepository.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
