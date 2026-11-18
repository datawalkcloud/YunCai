# 云财（YunCai）

前后端分离的会计记账系统：.NET 10 模块化单体 + PostgreSQL + React 19 SPA。覆盖账套、科目、凭证、账簿、财务报表、资金出纳、期末结转结账、固定资产、发票、工资、票据、电子档案，以及智能报税取数。

界面主体为风格复刻页，与早期实页并存，接的是同一套真实 API。项目采用**旧准则（2001 版《企业会计制度》）**科目编码体系，同时兼容新准则编码。

> 开发约定、会计口径与踩坑记录集中在 [`AGENTS.md`](AGENTS.md)，动手改代码前建议先读。

## 功能一览

| 领域 | 模块 | 主要能力 |
| --- | --- | --- |
| 账套与基础档案 | `AccountSets` `Accounting` `Assist` | 多账套、币别、凭证字、重新初始化、科目层级与期初、七类辅助核算 |
| 凭证 | `Vouchers` | 录入/导入（NPOI 读 .xls）、模板、反过账、保存即过账 |
| 账簿 | `Ledgers` | 总账、明细账、多栏账等，统一走 `ILedgerSource` + `LedgerDataSetFactory` 投影取数 |
| 报表 | `Reports` | 科目余额表、资产负债表、利润表/季报、现金流量表、部门/项目利润表、费用明细、所有者权益变动、纳税统计、自定义报表 |
| 资金出纳 | `Funds` | 日记账、收支类别与关键字智能匹配、内部转账、批量生成凭证、资金报表与总账核对 |
| 期末 | `Closing` | 损益结转、自定义结转模板、批量结账、撤销结转 |
| 资产与其他 | `FixedAssets` `Invoices` `Salary` `Archives` `Budgets` `Expenses` | 固定资产、发票、工资、电子档案分类树与附件、预算、费用 |
| 智能报税 | `Tax` | 税费种/申报/缴款/完税证明取数、一键取票、个税四页、ITS 查询统计（直连目前仅在浙江验证） |
| 审计与身份 | `Auditing` `Identity` | 操作日志、用户与 JWT 认证 |

## 技术栈

| 层 | 选型 |
| --- | --- |
| 后端 | ASP.NET Core / .NET 10、MediatR、Serilog、JWT Bearer、EF Core + Npgsql |
| 数据库 | PostgreSQL 17 |
| Excel / PDF | MiniExcel（.xlsx）、NPOI 2.8（.xls 凭证）、PaperSharp（PDF） |
| 前端 | React 19、TypeScript 6、Vite 8（rolldown）、antd v6、Tailwind CSS 4、react-router 8、React Compiler |
| API 客户端 | openapi-generator（typescript-fetch），产物在 `ui/src/apis-gen/` |
| 检查 | oxlint（前端）、xUnit + coverlet（后端） |

## 目录结构

```text
.
├── docs/                        设计、调研、部署、测试规范
├── src/
│   ├── YunCai.slnx
│   ├── YunCai/                  后端主项目
│   │   ├── Modules/<上下文>/    Domain / Application / Infrastructure / Api
│   │   ├── Shared/Infrastructure/   AppDbContext 与全局基础设施
│   │   ├── Migrations/          EF Core 迁移（当前 100+ 个）
│   │   └── Program.cs           组合根：DI 注册与运行时基线
│   ├── YunCai.Tests/            后端单元测试
│   └── TaxAssistant/            税局辅助工具
├── ui/
│   ├── src/apis-gen/            生成代码，禁止手改
│   ├── src/api/                 手写 API 封装（auth、accountSets）
│   ├── src/pages/business/      业务页面（正式 UI）
│   ├── src/pages/               既有实页（vouchers、ledger、reports…）
│   └── src/accountSets/         当前账套 Context
├── scripts/                     覆盖率报告等辅助脚本
├── .github/workflows/           CI 与镜像发布
├── docker-compose.yml           本地/单机部署：db + migrate + api + web
├── docker-compose.acr.yml       从 ACR 拉镜像的覆盖层
└── AGENTS.md                    开发约定、领域须知、常见陷阱
```

模块边界：跨模块只能引用对方 **Domain**，Application / Infrastructure 不跨模块引用。新业务放进对应模块，不要回填到根级分层。

## 本机开发

前置条件：.NET 10 SDK、Node.js 24、PostgreSQL 17（本地或 Docker）。

### 1. 配置后端

在 `src/YunCai/` 下创建 `appsettings.Development.json`（该文件已被 `.gitignore` 排除，不要提交）。仓库文档里提到的 `appsettings.Development.example.json` 模板当前缺失，可直接用下面这份最小配置起步：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=yuncai;Username=postgres;Password=你的数据库密码"
  },
  "Jwt": {
    "Key": "换成至少32位的随机字符串"
  },
  "YunCai": {
    "Database": { "AutoMigrate": true },
    "Networking": {
      "HttpsRedirection": false,
      "UseHsts": false
    }
  }
}
```

随机密钥可用 `openssl rand -base64 48 | tr -d '/+='` 生成。连接串为空或 `Jwt:Key` 缺失/短于 32 位时，进程会**直接启动失败**——这是设计行为。系统不预置任何账号：起来后在登录页注册手机号，再按建账引导建账套。开发环境必须关掉 HTTPS 跳转，否则本地 http 访问会被打断。

### 2. 启动服务

```bash
# 终端 1：后端 http://localhost:5271
cd src/YunCai
dotnet run

# 终端 2：前端 http://localhost:5173，/api 代理到 5271 并去掉 /api 前缀
cd ui
npm install
npm run dev
```

用完整的 `dotnet run` 启动（不要加 `--no-build`），否则可能遇到 EF 的 `PendingModelChangesWarning`。Windows PowerShell 不支持 `&&`，请按上面分行的形式执行。

### 3. 重新生成 API 客户端

后端 API / DTO 有改动后，保持 5271 在运行，然后：

```bash
cd ui
npm run gen
```

OpenAPI 文档只在 Development 环境暴露（`/openapi/v1.json`）。`ui/src/apis-gen/` 由生成器覆盖，任何手改都会丢失。

### 4. 测试

```bash
# 后端测试
dotnet test src/YunCai.Tests/YunCai.Tests.csproj --nologo

# 覆盖率（CI 同款口径，剔除 EF Migrations）
dotnet test src/YunCai.Tests/YunCai.Tests.csproj --nologo \
  --collect:"XPlat Code Coverage" --settings coverlet.runsettings --results-directory .coverage
node scripts/coverage-report.mjs --gate 40

# 前端检查
cd ui
npm run lint
npm run build
```

前端类型检查可单独跑 `npx tsc -b tsconfig.app.json`。覆盖率门禁与口径见 [`docs/测试规范.md`](docs/测试规范.md)。

## 部署

- **Docker Compose（推荐起步）**：`cp .env.example .env`，填好 `YUNCAI_DB_PASSWORD`、`YUNCAI_JWT_KEY`，`docker compose up -d --build`；`migrate` 容器先跑 `--migrate-only` 建结构，主服务起来后在登录页注册账号、按建账引导建账套。`api` 不映射宿主机端口，外部只能经 nginx 访问，这是登录限流按真实 IP 生效的前提。
- **镜像发布**：推 `v*` 标签触发 `.github/workflows/release-image.yml`，构建并推送 `api` / `web` 镜像到 ACR；服务器用 `docker-compose.acr.yml` 按版本号拉取与回滚。

完整步骤、环境变量、TLS、备份恢复与排障见 [`docs/部署上线说明.md`](docs/部署上线说明.md)。

## 文档索引

| 文档 | 内容 |
| --- | --- |
| [`AGENTS.md`](AGENTS.md) | 编码约定、模块边界、会计口径、常见陷阱（必读） |
| [`docs/部署上线说明.md`](docs/部署上线说明.md) | Compose / ACR 部署、配置、TLS、备份 |
| [`docs/测试规范.md`](docs/测试规范.md) | 测试分层与覆盖率棘轮 |
| [`docs/设计-辅助核算与账簿.md`](docs/设计-辅助核算与账簿.md) | 辅助核算与账簿设计 |
| [`docs/J6功能调研.md`](docs/J6功能调研.md) [`docs/J6功能差距清单.md`](docs/J6功能差距清单.md) [`docs/J6全页调研与云财路线图.md`](docs/J6全页调研与云财路线图.md) | 对标调研与路线图 |
| [`docs/税局取数直连与页面路径.md`](docs/税局取数直连与页面路径.md) | 税局直连的报文形态、待调查项 |
| [`docs/小企业会计准则-会计科目表.md`](docs/小企业会计准则-会计科目表.md) | 科目编码参考 |

## 已知边界

- 尚无用户↔账套授权与角色权限：登录用户对可见账套都有完整读写权，当前只适合单人 / 可信内部使用。
- 凭证、日记账等列表接口全量返回，服务端没有分页，单期数据量大时首屏会变慢。
- 上传上限 32MB，电子档案附件以 Base64 存库，账套多了会明显膨胀数据库。
- 前端单包约 1.4MB（gzip 365KB），尚未做路由级分包。
- 智能报税直连仅在浙江验证；个税 ITS 明文链路只跑通编译，真机（需短信登录）尚未验证明文回流。
- `ui/src/pages/business/*` 与 `ui/src/pages/{vouchers,ledger,funds,reports,settings,closing}` 两套页面并存，数据链路接法不同；改取数前先确认改的是哪一套。`pages/home`、`pages/vouchers/import` 是不可达的孤儿代码。
