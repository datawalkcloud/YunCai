// 生成 PresetChart.cs（建账预置科目清单，同时也是 preset_accounts 表的数据来源）。
//
// 四套清单的数据快照都在 scripts/data/ 下，改清单就改 JSON 再重跑本脚本：
//   preset_chart_enterprise.json   企业会计准则：原 DefaultChartOfAccounts 的 257 行快照（2001 版编码体系：权益 3xxx、成本 4xxx、损益 5xxx/6xxx）
//   preset_chart_kingdee.json      小企业会计准则：金蝶导出科目表（一级已与财会〔2011〕17 号附录逐条比对）
//   preset_chart_2006.json         新会计准则（2006 年颁）：财会〔2006〕18 号附录 A 编码体系（权益 4xxx、成本 5xxx、损益 6xxx）
//   preset_chart_nonprofit.json    民间非营利组织会计制度：净资产 3xxx、收入 4xxx、费用 5xxx
// 三份外部清单的原始形态是 xlsx（列：编码 / 名称 / 类别 / 余额方向 / 辅助核算类别 / 是否现金科目 / 状态 / 是否平行科目），
// 快照只取前四列；辅助核算、现金科目标记、平行科目三列预置阶段不落库。
//
// 用法：node scripts/gen_preset_chart.mjs [repoRoot]
// 重跑后必须 dotnet ef migrations add <名称> 生成一次数据迁移，否则库里不会多出这些行。
import { readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const repo = process.argv[2] ?? '.';
const dataDir = path.join(repo, 'scripts/data');

// 文件里的细分口径 → AccountCategory（1 资产 / 2 负债 / 3 权益 / 4 成本 / 5 损益）。
// 本系统没有「共同类」与「净资产」这两个类别：共同类科目（衍生工具、套期工具等）按借方余额归资产，
// 民非的净资产归权益（报表侧本就把权益与净资产同列处理）。
const CATEGORY_BY_TEXT = {
  资产: 1, 流动资产: 1, 非流动资产: 1, 共同: 1,
  负债: 2, 流动负债: 2, 非流动负债: 2,
  所有者权益: 3, 权益: 3, 净资产: 3,
  成本: 4,
  损益: 5, 收入: 5, 支出: 5, 营业收入: 5, 其他收益: 5, 营业成本及税金: 5,
  期间费用: 5, 其他损失: 5, 所得税: 5, 以前年度损益调整: 5,
};

const CHARTS = [
    { prop: 'Enterprise', standard: 'Enterprise', file: 'preset_chart_enterprise.json', cutpoints: [4, 7, 9], doc: '企业会计准则（预置沿用 2001 版《企业会计制度》编码体系），与柠檬云新建账套生成的科目表一致。' },
    { prop: 'SmallBusiness', standard: 'SmallBusiness', file: 'preset_chart_kingdee.json', cutpoints: [4, 6], doc: '小企业会计准则：一级科目照财政部财会〔2011〕17 号附录，明细科目取金蝶导出的既有账套。' },
    { prop: 'NewEnterprise2006', standard: 'NewEnterprise2006', file: 'preset_chart_2006.json', cutpoints: [4, 6], doc: '新会计准则（2006 年颁）：财会〔2006〕18 号附录 A 的编码体系，权益 4xxx、成本 5xxx、损益 6xxx。' },
    { prop: 'NonProfit', standard: 'NonProfit', file: 'preset_chart_nonprofit.json', cutpoints: [4, 6], doc: '民间非营利组织会计制度：净资产 3xxx、收入 4xxx、费用 5xxx。' },
];

// 上级按编码长度推：取小于本级长度的最大切点（4-2-2 → 4/6/8；4-3-2-2 → 4/7/9/11），
// 与 AccountPresetSeeder 建树时的父级口径一致，三级科目不能平铺到一级下面。
function parentByLengths(code, cutpoints) {
  for (const len of [...cutpoints].sort((a, b) => b - a)) {
    if (code.length > len) return code.slice(0, len);
  }
  return '';
}

function loadChart(spec) {
  const raw = JSON.parse(readFileSync(path.join(dataDir, spec.file), 'utf8'));
  return raw.map((row) => {
    const category = row.category ?? CATEGORY_BY_TEXT[row.categoryText];
    if (category == null) throw new Error(`${spec.file}：类别无法映射「${row.categoryText}」（科目 ${row.code} ${row.name}）`);
    const direction = row.direction ?? (row.directionText === '贷' ? 2 : 1);
    return {
      code: row.code,
      name: row.name,
      category,
      direction,
      parent: parentByLengths(row.code, spec.cutpoints),
    };
  });
}

const charts = CHARTS.map((spec) => ({ spec, rows: loadChart(spec) }));

// ── 校验：财政部附录的一级科目是否都在小企业清单里 ──
const small = charts.find((c) => c.spec.standard === 'SmallBusiness').rows;
try {
  const doc = readFileSync(path.join(repo, 'docs/小企业会计准则-会计科目表.md'), 'utf8');
  const official = [...doc.matchAll(/\|\s*\d+\s*\|\s*(\d{4})\s*\|\s*([^|]+?)\s*\|/g)].map((m) => ({ code: m[1], name: m[2] }));
  const byCode = new Map(small.filter((r) => r.code.length === 4).map((r) => [r.code, r.name]));
  const missing = official.filter((o) => !byCode.has(o.code));
  const diff = official.filter((o) => byCode.has(o.code) && byCode.get(o.code) !== o.name);
  console.log(`财政部附录一级科目：${official.length} 条；小企业清单一级：${byCode.size} 条`);
  console.log('附录有、清单没有：', missing.map((o) => `${o.code} ${o.name}`).join('、') || '（无）');
  console.log('名称不一致：', diff.map((o) => `${o.code} 附录「${o.name}」vs 清单「${byCode.get(o.code)}」`).join('；') || '（无）');
} catch (e) {
  console.log('（未找到财政部附录文档，跳过交叉校验：' + e.message + '）');
}

// ── 校验：重复编码 / 上级缺失 / 父子类别不一致 / 顶级编码长度 ──
let problems = 0;
for (const { spec, rows } of charts) {
  const codes = new Set(rows.map((r) => r.code));
  const dup = rows.length - codes.size;
  const orphans = rows.filter((r) => r.parent && !codes.has(r.parent));
  const byCode = new Map(rows.map((r) => [r.code, r]));
  // Account.AddChild 让明细科目的类别继承上级，清单里父子类别不同会被静默改写，必须报出来。
  const catDiff = rows.filter((r) => r.parent && byCode.has(r.parent) && byCode.get(r.parent).category !== r.category);
  const topWrong = rows.filter((r) => !r.parent && r.code.length !== 4);
  for (const o of orphans) o.parent = ''; // 上级缺失的行按一级处理，避免建树时退化成孤儿子级
  problems += dup + catDiff.length + topWrong.length;
  console.log(
    `${spec.standard}: ${rows.length} 行` +
    `｜重复编码 ${dup}` +
    `｜上级缺失 ${orphans.length}` +
    `｜父子类别不一致 ${catDiff.length}` +
    `｜顶级编码非 4 位 ${topWrong.length}`,
  );
  for (const d of catDiff.slice(0, 10)) console.log(`  类别冲突：${d.code} ${d.name}(${d.category}) 父 ${d.parent}(${byCode.get(d.parent).category})`);
  for (const t of topWrong.slice(0, 10)) console.log(`  顶级长度：${t.code} ${t.name}`);
}
if (problems > 0) console.log(`!! 共 ${problems} 处需要处理`);

const emitRows = (rows) =>
  rows
    .map((r) => `        new Item("${r.code}", "${r.name}", ${r.category}, ${r.direction}, "${r.parent}"),`)
    .join('\n');

const lists = charts
  .map(({ spec, rows }) => `        /// <summary>${spec.doc}</summary>
        public static IReadOnlyList<Item> ${spec.prop} { get; } =
        [
${emitRows(rows)}
        ];`)
  .join('\n\n');

const switchArms = charts.map(({ spec }) => `            AccountingStandard.${spec.standard} => ${spec.prop},`).join('\n');
const seedArms = charts.map(({ spec }) => `                         (AccountingStandard.${spec.standard}, ${spec.prop}),`).join('\n');

const body = `// 本文件的数据段由 scripts/gen_preset_chart.mjs 从 scripts/data/ 下的四份清单快照生成，请勿手改生成内容。
using YunCai.Modules.AccountSets.Domain;

namespace YunCai.Modules.Accounting.Domain
{
    /// <summary>
    /// 各会计准则的建账预置科目清单。这份清单是 <c>preset_accounts</c> 表的数据来源：
    /// 由 <see cref="SeedRows"/> 交给 EF 的 HasData，随迁移写入库；运行时建账只读表，不回落到代码。
    /// 要增删科目，改 scripts/data/ 里的清单再跑 <c>node scripts/gen_preset_chart.mjs</c> 重新生成，
    /// 然后 <c>dotnet ef migrations add</c> 补一次数据迁移；直接改库的话下一次迁移比对会把改动当成差异。
    /// </summary>
    public static class PresetChart
    {
        /// <summary>预置科目行：编码、名称、类别（AccountCategory）、余额方向（BalanceDirection）、上级科目编码（一级为空）。</summary>
        public readonly record struct Item(string Code, string Name, int Category, int Direction, string ParentCode = "");

${lists}

        /// <summary>按账套准则取清单；准则没有预置表时返回空。</summary>
        public static IReadOnlyList<Item> For(AccountingStandard standard) => standard switch
        {
${switchArms}
            _ => [],
        };

        /// <summary>展平成带稳定主键的实体行，供 AppDbContext 的 HasData 使用。</summary>
        public static IEnumerable<PresetAccount> SeedRows()
        {
            var id = 0;
            foreach (var (standard, rows) in new[]
                     {
${seedArms}
                     })
            {
                var order = 0;
                foreach (var row in rows)
                {
                    var hasParent = row.ParentCode.Length > 0;
                    yield return new PresetAccount(
                        ++id,
                        standard,
                        row.Code,
                        row.Name,
                        (AccountCategory)row.Category,
                        (BalanceDirection)row.Direction,
                        hasParent ? row.ParentCode : null,
                        ++order);
                }
            }
        }
    }
}
`;

const outPath = path.join(repo, 'src/YunCai/Modules/Accounting/Domain/PresetChart.cs');
writeFileSync(outPath, body, 'utf8');
const total = charts.reduce((n, c) => n + c.rows.length, 0);
console.log('已写出', outPath, `（共 ${total} 行：${charts.map((c) => `${c.spec.standard} ${c.rows.length}`).join('、')}）`);
