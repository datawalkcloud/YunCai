# AGENTS.md

面向 AI 编码助手 / 新成员的项目指南。KuaiJi（快记）是一个前后端分离的会计记账系统：ASP.NET Core 后端 + React SPA 前端，支持账套、科目、记账凭证、日记账及报表（科目余额表、资产负债表、现金日记账、利润表）。

## 技术栈

| 部分 | 技术 |
| --- | --- |
| 后端 | .NET 10 / ASP.NET Core Web API、EF Core + PostgreSQL（Npgsql）、MediatR、Serilog、JWT Bearer 认证 |
| Excel | MiniExcel（.xlsx 读取）、NPOI 2.8（.xls 凭证解析，csproj 中已接受 OSMF EULA） |
| 前端 | React 19 + TypeScript、Vite 8（rolldown）、Ant Design 6、Tailwind CSS 4、react-router 8、React Compiler（babel 插件） |
| API 客户端 | openapi-generator（typescript-fetch），产物在 `ui/src/apis-gen/` |
| Lint | oxlint（前端） |

## 目录结构

```
src/KuaiJi/            后端（单项目分层）
  Controllers/         API 控制器（Auth、AccountSets、Accounts、Vouchers、Reports）
  Application/         应用服务、DTO、导入服务（AccountSets/Accounts/Journals/Vouchers/Reports）
  Domain/              实体、枚举、仓储接口（按聚合分目录）
  Infrastructure/      仓储实现、DbContext、Excel 读取器实现
  Services/            JwtTokenService、PasswordHasher
  Migrations/          EF Core 迁移（启动时自动执行 Migrate）
ui/                    前端
  src/apis-gen/        openapi-generator 生成代码，禁止手改，重新生成覆盖
  src/api/             手写 API 封装
  src/pages/           按业务模块分页面（vouchers、reports、ledger、funds、settings 等）
  src/accountSets/     当前账套 Context
  src/auth/            登录态 Context
```

## 常用命令

```powershell
# 后端（工作目录 src/KuaiJi）
dotnet build
dotnet run                    # 监听 http://localhost:5271（launchSettings http profile）
dotnet ef migrations add <名称>   # 修改实体后新增迁移；启动时自动应用，无需手动 database update

# 前端（工作目录 ui）
npm run dev                   # Vite 开发服务器 http://localhost:5173，/api 代理到 5271 并去掉 /api 前缀
npm run build                 # tsc -b && vite build
npm run lint                  # oxlint
npm run gen                   # 重新生成 apis-gen（要求后端已在 5271 运行，读取 /openapi/v1.json）
```

- shell 是 Windows PowerShell，**不支持 `&&`**，用 `;` 分隔命令。
- `dotnet build` 报 MSB3021/MSB3027（KuaiJi.exe 被占用）时，先执行
  `Stop-Process -Name "KuaiJi" -Force -ErrorAction SilentlyContinue` 再构建。

## 开发工作流

1. 后端改动 API（Controller / DTO）后：`dotnet run` 启动后端 → 在 `ui/` 下执行 `npm run gen` 重新生成前端客户端 → 前端使用 `apis-gen` 中的强类型 API。
2. OpenAPI 仅在 Development 环境暴露（`/openapi/v1.json`）；开发环境不做 HTTPS 跳转，方便生成器直连 http。
3. 数据库连接串在 `appsettings.json` 的 `ConnectionStrings:DefaultConnection`（PostgreSQL）。应用启动时自动迁移，并初始化默认用户（admin/user）与默认账套。
4. 科目、日记账、凭证均归属于某一账套（AccountSet），接口普遍需要 accountSetId 上下文。

## 编码约定

- 后端分层：Controller 只做参数绑定与转发，业务逻辑放 `Application/` 服务，数据访问经 `Domain/` 仓储接口 + `Infrastructure/` 实现；新服务需在 `Program.cs` 注册 DI。
- 注释、提交信息、面向用户的文案使用中文；代码风格与周边保持一致。
- 前端路径别名 `@` 指向 `ui/src`。
- 前端请求统一走 `/api` 前缀（Vite 代理会剥掉该前缀转发到后端）。
- 不要复制粘贴大段相似代码，优先抽取复用。

## 领域须知（会计）

- 项目实际使用**旧准则**（2001 版《企业会计制度》）科目编码体系，如实收资本为 `3001`（权益类）。
- 新旧准则编码首位含义不同（旧：3=权益、4=成本；新：4=权益、5=成本），涉及 `InferCategoryAndDirection`、`BalanceSheetService` 等按编码首位/前四位分类的逻辑时，务必兼容两套体系，避免权益类被误判为成本类。
- 科目编码有层级（编码方案控制各级位数，最多 10 级）；批量创建/导入科目时必须按编码长度排序，先建父级再建子级。

## 常见陷阱

- **MiniExcel 读 .xlsx**：`Query()` 必须显式传 `excelType: ExcelType.XLSX`，否则报 "The file type could not be inferred automatically"。
- **FormData 枚举参数**：前端用 `FormData.append()` 提交的枚举/标量参数，控制器参数必须加 `[FromForm]`，否则默认从 query string 绑定、永远是默认值。
- **apis-gen 目录**：由 openapi-generator 生成，任何手改都会在下次 `npm run gen` 时丢失。
- **凭证 .xls 导入**：走 NPOI 解析（`XlsVoucherImportReader`）；科目 .xlsx 导入走 MiniExcel（`XlsxAccountImportReader`），两条链路依赖库不同。
