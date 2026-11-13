namespace YunCai.Modules.AccountSets.Domain
{
    /// <summary>角色权限目录里的一个操作（模块内可勾选的最小权限单元）。</summary>
    public sealed record RolePermissionAction(string Key, string Name);

    /// <summary>角色权限目录里的一个功能模块。</summary>
    public sealed record RolePermissionModule(string Key, string Name, IReadOnlyList<RolePermissionAction> Actions);

    /// <summary>预置角色定义：名称、是否账套管理员（不可编辑）、初始权限键。</summary>
    public sealed record RolePreset(string Name, bool IsAdmin, IReadOnlyList<string> Keys);

    /// <summary>
    /// 角色权限目录：功能模块 × 操作。权限键格式为「模块.操作」（如 vouchers.view）。
    /// 汇总文案按模块归并，只有「查看」权限的连续模块合并为「查看资金、账簿和报表」。
    /// </summary>
    public static class RolePermissionCatalog
    {
        private static RolePermissionAction Act(string key, string name) => new(key, name);

        private static RolePermissionModule Mod(string key, string name, params RolePermissionAction[] actions)
            => new(key, name, actions);

        /// <summary>全部模块与操作，顺序即权限编辑弹窗的展示顺序。</summary>
        public static readonly IReadOnlyList<RolePermissionModule> Modules =
        [
            Mod("vouchers", "凭证",
                Act("view", "查看"), Act("edit", "编辑"), Act("import", "导入"),
                Act("export", "导出"), Act("delete", "删除"), Act("print", "打印")),
            Mod("funds", "资金",
                Act("view", "查看"), Act("edit", "编辑"), Act("import", "导入"),
                Act("export", "导出"), Act("print", "打印")),
            Mod("invoices", "发票",
                Act("view", "查看"), Act("edit", "编辑"), Act("import", "导入"), Act("export", "导出")),
            Mod("salary", "工资",
                Act("view", "查看"), Act("edit", "编辑")),
            Mod("assets", "资产",
                Act("view", "查看"), Act("edit", "编辑")),
            Mod("tax", "税务",
                Act("view", "查看"), Act("edit", "编辑")),
            Mod("closing", "期末结转",
                Act("view", "查看"), Act("edit", "编辑")),
            Mod("ledgers", "账簿",
                Act("view", "查看"), Act("export", "导出"), Act("print", "打印")),
            Mod("reports", "报表",
                Act("view", "查看"), Act("export", "导出"), Act("print", "打印")),
            Mod("accountset-add", "新增账套", Act("execute", "操作")),
            Mod("accountset-delete", "删除账套", Act("execute", "操作")),
            Mod("backup", "备份恢复", Act("view", "查看"), Act("edit", "备份 / 恢复")),
            Mod("reinit", "重新初始化账套", Act("execute", "操作")),
            Mod("link-inventory", "关联进销存", Act("execute", "操作")),
            Mod("link-invoice", "关联云发票", Act("execute", "操作")),
        ];

        /// <summary>全部权限键。</summary>
        public static readonly IReadOnlyList<string> AllKeys =
            Modules.SelectMany(m => m.Actions.Select(a => $"{m.Key}.{a.Key}")).ToList();

        /// <summary>账套首次读取角色时播种的预置角色。</summary>
        public static readonly IReadOnlyList<RolePreset> Presets = BuildPresets();

        /// <summary>过滤掉未知权限键并去重，保证入库内容始终来自目录。</summary>
        public static IReadOnlyList<string> Normalize(IEnumerable<string>? keys)
        {
            if (keys is null)
            {
                return [];
            }

            var known = AllKeys.ToHashSet(StringComparer.Ordinal);
            return keys
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Select(k => k.Trim())
                .Where(known.Contains)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>把权限键集合汇总成截图里的权限说明（如「查看资金、账簿和报表 | 新增账套」）。</summary>
        public static string BuildSummary(IEnumerable<string>? keys)
        {
            var granted = Normalize(keys).ToHashSet(StringComparer.Ordinal);
            if (granted.Count == 0)
            {
                return "无权限";
            }

            // 与柠檬云的展示口径一致：先核心业务模块（只勾「查看」的模块合并成一段），
            // 再是「查看…」合并段，最后是新增账套 / 删除账套 / 备份恢复等设置类动作。
            var coreParts = new List<string>();
            var viewOnly = new List<string>();
            foreach (var module in Modules.Where(m => IsCoreModule(m.Key)))
            {
                var grantedActions = module.Actions
                    .Where(a => granted.Contains($"{module.Key}.{a.Key}"))
                    .Select(a => a.Key)
                    .ToList();
                if (grantedActions.Count == 0)
                {
                    continue;
                }

                if (module.Actions.Count > 1 && grantedActions.Count == 1 && grantedActions[0] == "view")
                {
                    viewOnly.Add(module.Name);
                }
                else
                {
                    coreParts.Add(module.Name);
                }
            }

            if (viewOnly.Count > 0)
            {
                coreParts.Add(viewOnly.Count == 1
                    ? $"查看{viewOnly[0]}"
                    : $"查看{string.Join("、", viewOnly.Take(viewOnly.Count - 1))}和{viewOnly[^1]}");
            }

            foreach (var module in Modules.Where(m => !IsCoreModule(m.Key)))
            {
                if (module.Actions.Any(a => granted.Contains($"{module.Key}.{a.Key}")))
                {
                    coreParts.Add(module.Name);
                }
            }

            return string.Join(" | ", coreParts);
        }

        private static bool IsCoreModule(string key)
            => key is "vouchers" or "funds" or "invoices" or "salary" or "assets"
                or "tax" or "closing" or "ledgers" or "reports";

        private static IReadOnlyList<RolePreset> BuildPresets()
        {
            static string[] AllOf(RolePermissionModule m) => m.Actions.Select(a => a.Key).ToArray();

            static string[] ViewOf(RolePermissionModule m) => ["view"];

            static IReadOnlyList<string> Keys(Func<RolePermissionModule, string[]?> selector)
            {
                var keys = new List<string>();
                foreach (var module in Modules)
                {
                    var actions = selector(module);
                    if (actions is null)
                    {
                        continue;
                    }

                    keys.AddRange(actions.Select(a => $"{module.Key}.{a}"));
                }

                return keys;
            }

            return
            [
                new RolePreset("账套管理员", true, AllKeys),
                new RolePreset("主管", false, Keys(m => m.Key is "vouchers" or "funds" or "invoices" or "salary"
                    or "assets" or "tax" or "closing" or "ledgers" or "reports"
                    ? AllOf(m)
                    : m.Key == "accountset-add" ? AllOf(m) : null)),
                new RolePreset("制单人", false, Keys(m => m.Key is "vouchers" or "invoices" or "salary"
                    or "assets" or "tax" or "closing"
                    ? AllOf(m)
                    : m.Key is "funds" or "ledgers" or "reports"
                        ? ViewOf(m)
                        : m.Key == "accountset-add" ? AllOf(m) : null)),
                new RolePreset("出纳", false, Keys(m => m.Key == "funds"
                    ? AllOf(m)
                    : m.Key is "vouchers" or "assets" or "ledgers" or "reports"
                        ? ViewOf(m)
                        : m.Key == "accountset-add" ? AllOf(m) : null)),
                new RolePreset("查看", false, Keys(m => m.Key is "vouchers" or "funds" or "assets" or "ledgers" or "reports"
                    ? ViewOf(m)
                    : m.Key == "accountset-add" ? AllOf(m) : null)),
            ];
        }
    }
}
