# AGENTS.md

面向 AI 编码助手 / 新成员的项目指南。云财（YunCai）是一个前后端分离的会计记账系统：ASP.NET Core 后端（模块化 DDD）+ React SPA 前端，覆盖账套、科目、凭证、资金出纳、发票、工资、固定资产、票据、电子档案、期末结转结账及全套报表（科目余额表、资产负债表、利润表/季报、现金流量表、部门/项目利润表、自定义报表等）。

## 技术栈

| 部分 | 技术 |
| --- | --- |
| 后端 | .NET 10 / ASP.NET Core Web API、EF Core + PostgreSQL（Npgsql）、MediatR、Serilog、JWT Bearer 认证 |
| Excel | MiniExcel（.xlsx 读取）、NPOI 2.8（.xls 凭证解析，csproj 中已接受 OSMF EULA） |
| 前端 | React 19 + TypeScript、Vite 8（rolldown）、shadcn/radix-ui + lucide-react、Tailwind CSS 4、react-router 8、React Compiler（babel 插件） |
| API 客户端 | openapi-generator（typescript-fetch），产物在 `ui/src/apis-gen/` |
| Lint | oxlint（前端） |

## 目录结构（模块化 DDD）

```
src/YunCai/                      后端（单项目，按限界上下文分模块；API 契约与旧版兼容）
  Modules/<上下文>/               每个模块内分 Domain / Application / Infrastructure / Api
    Identity/                     用户与认证（User、Jwt、PasswordHasher）
    AccountSets/                  账套 + 币别/凭证字/重新初始化
    Accounting/                   科目与期初
    Assist/                       辅助核算档案（七类 AssistType）
    Vouchers/                     凭证 + 凭证模板
    Funds/                        日记账/收支类别/内部转账/票据（原 Journals+Drafts）
    Ledgers/                      账簿查询（纯读：ILedgerSource + LedgerDataSetFactory）
    Reports/                      财务报表（含现金流分类器 Domain/CashFlow、自定义报表聚合）
    Closing/                      期末结转与结账
    FixedAssets/ Invoices/ Salary/ Archives/ Auditing   资产/发票/工资/电子档案/操作日志
  Shared/Infrastructure/          AppDbContext（28+ DbSet，全部实体配置）、LenientDateOnlyModelBinder
  Program.cs                      组合根：全部 DI 注册在此（按模块全名 AddScoped）
  Migrations/                     EF Core 迁移（启动时自动执行 Migrate）
ui/                              前端
  src/apis-gen/                   openapi-generator 生成代码，禁止手改，重新生成覆盖
  src/api/                        手写 API 封装（auth、accountSets）
  src/pages/                      既有实页（vouchers、reports、ledger、funds、settings、closing、dashboard）
  src/pages/lemon/                柠檬云风格页面（正式 UI 的一部分，已接真实 API；侧边栏菜单在
                                  components/layout/dashboard/DashboardLayout.tsx，与实页互补共存）
  src/accountSets/                当前账套 Context（useCurrentAccountSetId 从路由取账套）
  src/auth/                       登录态 Context
```

模块边界约定：跨模块只能引用对方 **Domain**（如 Accounting.Domain 的枚举）；Application/Infrastructure 不跨模块引用；新增业务一律放进对应模块（没有就新建），不要回填到根级分层。

## 常用命令

```powershell
# 后端（工作目录 src/YunCai）
dotnet build
dotnet run                    # 监听 http://localhost:5271（launchSettings http profile，开发环境自动迁移 + 播种）
dotnet ef migrations add <名称>   # 修改实体后新增迁移
dotnet ef database update     # 手动应用迁移（生产环境的做法）；或 dotnet run -- --migrate-only

# 前端（工作目录 ui）
npm run dev                   # Vite 开发服务器 http://localhost:5173，/api 代理到 5271 并去掉 /api 前缀
npm run build                 # tsc -b && vite build
npm run lint                  # oxlint
npm run gen                   # 重新生成 apis-gen（要求后端已在 5271 运行，读取 /openapi/v1.json）

# 生产部署（仓库根目录，详见 docs/部署上线说明.md）
cp .env.example .env          # 必填 YUNCAI_DB_PASSWORD / YUNCAI_JWT_KEY / YUNCAI_ADMIN_PASSWORD
docker compose up -d --build  # web(nginx 托管 SPA 并反代 /api) + api + db；migrate 容器先跑 --migrate-only
                              # 也可由 .github/workflows/release-image.yml 推送 ACR，服务器用 docker-compose.acr.yml 拉取

# Kubernetes（Kustomize，清单在 deploy/k8s/，说明见 deploy/k8s/README.md）
kubectl apply -k deploy/k8s   # api 的 initContainer 先跑 --migrate-only 再启动；镜像取 ACR 的 v* 构建
```

- **本机机密**放 `src/YunCai/appsettings.Local.json`（从 `appsettings.Local.example.json` 复制，已被 .gitignore 排除）；`appsettings.json` 里不得出现真实连接串或签名密钥。配置优先级：appsettings.json < appsettings.{环境}.json < Local < 环境变量。
- shell 是 Windows PowerShell，**不支持 `&&`**，用 `;` 分隔命令。
- `dotnet build` 报 MSB3021/MSB3027（YunCai.exe 被占用）时，先执行
  `Stop-Process -Name "YunCai" -Force -ErrorAction SilentlyContinue` 再构建。

## 开发工作流

1. 后端改动 API（Controller / DTO）后：`dotnet run` 启动后端 → 在 `ui/` 下执行 `npm run gen` 重新生成前端客户端 → 前端使用 `apis-gen` 中的强类型 API。
2. OpenAPI 仅在 Development 环境暴露（`/openapi/v1.json`）；开发环境不做 HTTPS 跳转，方便生成器直连 http。
3. 数据库连接串走 `ConnectionStrings:DefaultConnection`（PostgreSQL），**真实值不入库**：本机放 `appsettings.Local.json`，容器放环境变量（见 `docs/部署上线说明.md`）。迁移与播种由配置开关控制：开发环境 `YunCai:Database:AutoMigrate=true` + `YunCai:Seed:Enabled=true`（admin/admin123、user/user123、默认账套）；生产由发布时的一次性任务 `dotnet YunCai.dll --migrate-only` 建结构——Compose 侧 `AutoMigrate` 保持关闭，K8s 侧 ConfigMap 置 `true`（单副本 + `Recreate`，主容器启动再补一次，避免漏跑迁移导致库结构落后）。连接串或 `Jwt:Key` 缺失/过短会直接让进程启动失败。
4. 科目、日记账、凭证均归属于某一账套（AccountSet），接口普遍需要 accountSetId 上下文。
5. 运行时基线（`Program.cs` 组合根）：未捕获异常 → Problem Details（`Shared/Infrastructure/Api/GlobalExceptionHandler`，业务校验类 400 透出中文文案，500 不外泄堆栈）；`/health` 探活含数据库连通；`Auth/login` 按来源 IP 限流 10 次/5 分钟；反向代理后必须开 `YunCai:Networking:TrustForwardedHeaders`，否则限流按代理 IP 计数。

## 编码约定

- 后端分层：Controller 只做参数绑定与转发，业务逻辑放 `Application/` 服务，数据访问经 `Domain/` 仓储接口 + `Infrastructure/` 实现；新服务需在 `Program.cs` 注册 DI。
- 注释、提交信息、面向用户的文案使用中文；代码风格与周边保持一致。
- 前端路径别名 `@` 指向 `ui/src`。
- 前端请求统一走 `/api` 前缀（Vite 代理会剥掉该前缀转发到后端）。
- 不要复制粘贴大段相似代码，优先抽取复用。

## 领域须知（会计）

- **账簿与报表只统计已过账凭证**（`VoucherStatus.Posted`）。没有审核环节：保存凭证即直接入账；草稿只来自导入时借贷不平衡的凭证或「反过账」退回，不入账，补齐后保存即入账。修改已过账凭证需先反过账。
- **查询区间一律归一到自然月**（起始日归到月初、截止日归到月末），见 `Modules/Ledgers/Application/LedgerDataSetFactory`；会计期间标识统一用 `yyyyMM` 整数。
- **余额内部一律按「借 - 贷」净额累计**（`BalanceAmounts`），只在输出层用 `SplitBySign` 拆回借/贷两栏；新增账页请沿用该口径，不要再引入按方向分别累加的写法。
- 账簿取数走 `Modules/Ledgers` 的 `ILedgerSource` + `LedgerDataSetFactory`（EF 投影查询 + 两级聚合）。**新增账页只做投影，不要各自读 `IVoucherRepository.GetAllAsync`**。
- 项目实际使用**旧准则**（2001 版《企业会计制度》）科目编码体系，如实收资本为 `3001`（权益类）。
- 新旧准则编码首位含义不同（旧：3=权益、4=成本；新：4=权益、5=成本），涉及 `InferCategoryAndDirection`、`BalanceSheetService` 等按编码首位/前四位分类的逻辑时，务必兼容两套体系，避免权益类被误判为成本类。
- 科目编码有层级（编码方案控制各级位数，最多 10 级）；批量创建/导入科目时必须按编码长度排序，先建父级再建子级。
- **期末结转与结账（`Modules/Closing`）**：先结转损益、再结账——账套 `CarriedPeriod`（已结转至）必须先于 `ClosedPeriod`（已结账至）推进；结转凭证由系统生成并直接过账（摘要「期末结转损益 / 结转未分配利润」，含 4103/3103 本年利润科目，识别与「撤销结转」均以此为准）；支持自定义结转模板（执行时按当月数据计算并校验平衡，模板凭证摘要用模板行摘要、不参与撤销识别）与批量结账；资产负债表把损益类余额并入未分配利润行、利润表剔除结转凭证，保证结转前后报表口径一致。
- **出纳闭环（`Modules/Funds`）**：资金账户（日记账）支持启停用与开户行信息，停用只拦新流水（记账 / 导入 / 内部转账），历史数据与报表照常；内部转账自动在两端各登记一笔流水（类别「内部转账」、往来单位留空，与柠檬云一致），已生成凭证的转账不可改删；批量生成凭证按合并方式（全部 / 日期 / 类别 / 方向）分组出票，对方科目优先取统一科目、其次取类别「默认对方科目」，任一流水无法确定对方科目则整单拒绝；收支类别可配智能匹配关键字（导入按摘要包含自动归类，同类取最长命中）；资金报表按 账户 / 类别 / 往来 汇总，出纳台账侧为全量流水（含未生成凭证），核对总账的总账侧仅统计已过账凭证 + 科目期初。
- **报表新口径（`Modules/Reports`）**：现金流量表为直接法简化工作底稿——现金科目=一级编码 1001/1002/1012，每张凭证的现金净收付按比例分摊到对手方科目，由 `Domain/CashFlow/CashFlowClassifier` 归类到标准 35 行主表（投资/筹资区分、权益类对手方归筹资），三种页面（月/季/标准）共用一套 DTO；利润表/部门/项目利润表/费用明细表的行次归集统一走 `IncomeLineMapper`（含新旧准则编码兼容），结转凭证（含 4103/3103）一律剔除；所有者权益变动表、纳税统计表、财务概要共享 `AccountNetCalculator`（借-贷净额含期初）；自定义报表（数据透视表）为真实聚合 `CustomReport`（表 custom_reports），渲染=科目×12 个月（余额/发生额两种口径）。

- **智能报税取数（`Modules/Tax`）**：税费种、申报记录、缴款信息、已开具完税证明都在已登录页面内直连数字账户的明文接口（`DescribeSfzrdxx` / `DescribeSbmxcx2` / `DescribeJkxxcx` / `DescribeWszmxx`，`pageSize=500`），被 `9999` 拒时按菜单开一次业务页补建子系统会话再重试，仍失败返回 `NeedLogin`——菜单不再是取数通道，只有税费种保留「点菜单 + 抓 JSON + 读渲染表格」的兜底；一键取票为「抓明文请求 → 改分页参数 → 页面内重发」，钩子只拿到 `Jmbw` 密文时退回逐页点「下一页」；应缴税款无独立接口，回落申报记录的应补（退）税额；个税四页分三种源：人员管理走「单位办税 → 点菜单查询 → 明文钩子/抓包/读渲染 el-table」（本地工资表档案打底，只用税局回的报送/验证状态两列覆盖），报表填写与申报缴款是 `IitReportService` 用本系统工资表按累计预扣法算、不碰税局，查询统计两页签都走 ITS 页面驱动（申报记录 `queryQySbjl`，缴税记录 `kjsbjsxxCx`，可按缴税日期或税款所属期过滤；缴税记录是扣缴端按人按次的流水，不能拿数字账户的企业端 `DescribeJkxxcx` 填）——ITS（`etax.chinatax.gov.cn`）的 `web/dkdj/*` 响应在网络层 100% 是整包密文（`dmnHafHu=`，0 次明文），但密文是页面自己解的，所以取数已在页面解密之后 tap `JSON.parse`（`TaxPortalPageScripts.IitPlainHook` / `IitPlainTake`，三路报文：明文钩子 → 网络层 JSON → 渲染表格），诊断端点 `POST /TaxPortal/{id}/debug/iit-plain` 同时用 `IitCryptoRecon` 摸清加解密落在哪个全局 / 同源脚本 / webpack 模块；这条路只在代码里跑通编译，真机（需收短信登录）尚未验证明文是否回流——下一步是拿 recon 结果在页面内调它自己的加密函数改分页重发，而不是在后端复刻算法；未申报查询只有诊断端点 `probe-undeclared`。直连只在浙江验证过。非 JSON 响应分四种形态（明文 JSON / `9999` 信封 / 整包密文 / 瑞数挑战页 HTML），先按形态判据分类再决定往哪边调查，待调查清单见 `docs/税局取数直连与页面路径.md`。

## 常见陷阱

- **MiniExcel 读 .xlsx**：`Query()` 必须显式传 `excelType: ExcelType.XLSX`，否则报 "The file type could not be inferred automatically"。
- **FormData 枚举参数**：前端用 `FormData.append()` 提交的枚举/标量参数，控制器参数必须加 `[FromForm]`，否则默认从 query string 绑定、永远是默认值。
- **apis-gen 目录**：由 openapi-generator 生成，任何手改都会在下次 `npm run gen` 时丢失。
- **凭证 .xls 导入**：走 NPOI 解析（`XlsVoucherImportReader`）；科目 .xlsx 导入走 MiniExcel（`XlsxAccountImportReader`），两条链路依赖库不同。
- **EF 投影后再过滤/分组无法翻译**：把分录投影成自定义记录（`select new EntryRow(...)`）之后，再对其 `.Where(日期)` 或 `.GroupBy` 会抛 "The LINQ expression could not be translated"。日期条件必须写在查询表达式的 `where` 里（聚合则直接对实体 `join` 后 `group e by ...`），见 `Modules/Ledgers/Infrastructure/LedgerSource`。
- **前端日期（DateOnly）序列化**：apis-gen 生成的 Date 参数按 `toISOString().substring(0,10)` 序列化（UTC），直接传本地 `new Date(2026, 0, 1)` 会在东八区回退一天；日期字段一律先构造 `new Date("YYYY-MM-DD")` 再交给生成的 API。
- **apis-gen 日期运行时是 Date 对象**：生成客户端的 `FromJSON` 会把日期字段 `new Date(...)` 解析成真正的 Date（类型与运行时一致），前端展示用 `new Date(x).toISOString().substring(0,10)` 得 yyyy-MM-dd，不要直接 `String(x).slice(0,10)`（会渲染出英文星期）。
- **改实体命名空间必须同步迁移快照**：`Migrations/AppDbContextModelSnapshot.cs` 以**实体全名字符串**（如 `"YunCai.Modules.Vouchers.Domain.Voucher"`）登记实体；改实体命名空间而不同步该文件，下次 `dotnet ef migrations add` 会生成全量删建。验证方法：加一个临时迁移看 Up/Down 是否为空，空则 `dotnet ef migrations remove` 撤掉。
- **oxlint 存量警告**：根目录 `npx oxlint` 有 ~15 条既有 `react(only-export-components)` fast-refresh 提示（shadcn 组件与 router 辅助组件），不属于本次改动；lemon 页面文件保持 0 警告。
- **启动即失败是设计**：`ConnectionStrings:DefaultConnection` 为空、或 `Jwt:Key` 缺失/短于 32 位时进程直接抛异常退出，不再「吞掉迁移错误照常服务」。CI 里跑 `dotnet ef` 需同时给这两个环境变量（见 `.github/workflows/ci.yml`）。`AutoMigrate=false` 的进程启动前还会比对迁移历史，有未应用迁移就拒绝启动并点名——`/health` 只验连通性，库结构落后时它照常 200 而接口按路径 500（如 `42703: column u.created_at does not exist`），别把探针绿了当成迁移跑过。
- **前端页面两套并存**：`ui/src/pages/lemon/*` 是复刻柠檬云的正式 UI，`ui/src/pages/{vouchers,ledger,funds,reports,settings,closing}` 是既有实页，功能重叠但**数据来源接法不同**——改数据链路时先确认改的是哪套，`pages/home`、`pages/vouchers/import`、`lemon/menu.ts` 属不可达孤儿代码。
