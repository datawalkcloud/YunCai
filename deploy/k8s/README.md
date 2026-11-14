# 云财 Kubernetes 部署

用 Kustomize 管理的最小部署：`api`（含一次性迁移）与 `web`（nginx + SPA）两个 Deployment，数据库默认走外部 PostgreSQL，也提供集群内 PostgreSQL 的可选清单。

```
deploy/k8s/
  kustomization.yaml      入口：命名空间、镜像地址与版本
  namespace.yaml
  domain.yaml             域名：Ingress 的 host 与 TLS hosts 都从这里注入
  configmap.yaml          非敏感配置（含 seed 开关、代理信任、TLS 终止方式）
  secret.example.yaml     连接串 / JWT 密钥 / 初始管理员口令（不要提交真值）
  api-deployment.yaml     api + 迁移 initContainer + /dev/shm
  api-service.yaml        Service 名必须是 api（web 的 nginx 按此反代）
  web-deployment.yaml     无状态，默认 2 副本
  web-service.yaml
  ingress.yaml            nginx Ingress，host 取自 domain.yaml
  postgres/               可选：集群内 PostgreSQL（StatefulSet + PVC）
```

## 前置条件

- 集群可用，`kubectl` 能连上；如果走 Ingress，集群里已有 Ingress Controller（示例按 nginx）。
- 镜像已由 GitHub Actions 推到 ACR（`v*` 标签触发，见 `.github/workflows/release-image.yml`）。

## 1. 镜像地址

`kustomization.yaml` 的 `images` 已指向本项目用的 ACR 个人实例：

```yaml
images:
  - name: yuncai-api
    newName: crpi-gr7j2r1czfy1t1uu.cn-hangzhou.personal.cr.aliyuncs.com/springhgui/yuncai-api
    newTag: 1.0.0
```

`yuncai-web` 同样处理。发布新版本只改 `newTag`（`1.0.0` / sha-xxxx / latest）。换账号时注意个人实例域名形如 `crpi-<实例ID>.<地域>.personal.cr.aliyuncs.com`，与旧版 `registry.<地域>.aliyuncs.com` 不是同一个端点（用错会报 `Forbidden Host`），命名空间要与仓库变量 `ACR_NAMESPACE` 一致。

## 2. 命名空间与拉取凭证

```bash
kubectl apply -f deploy/k8s/namespace.yaml

kubectl -n yuncai create secret docker-registry acr-pull \
  --docker-server=crpi-gr7j2r1czfy1t1uu.cn-hangzhou.personal.cr.aliyuncs.com \
  --docker-username=<ACR 用户名> \
  --docker-password=<ACR 固定密码>
```

本工作区已生成 `acr-pull-secret.yaml`（同样在 `.gitignore` 里），可直接 `kubectl apply -f deploy/k8s/acr-pull-secret.yaml`。

## 3. 应用密钥

`yuncai-secret` 至少包含三项：

```bash
kubectl -n yuncai create secret generic yuncai-secret \
  --from-literal=ConnectionStrings__DefaultConnection='Host=db;Port=5432;Database=yuncai_pro;Username=yuncai;Password=<口令>' \
  --from-literal=Jwt__Key="$(openssl rand -base64 48 | tr -d '/+=')" \
  --from-literal=YunCai__Seed__AdminPassword='<初始管理员口令>'
```

- 用外部数据库时把 `Host` 换成实际地址，按服务商要求补 `Port` / `SSL Mode=Require`；库不存在时首次迁移会自动创建。
- `Jwt__Key` 至少 32 位，轮换后所有人需重新登录。
- 也可以复制 `secret.example.yaml` 改好后 `kubectl apply -f`，注意不要提交真实值。
- 本工作区已按开发库生成 `secret.yaml`（连接串、JWT 密钥、初始口令），该文件在 `.gitignore` 里，直接 `kubectl apply -f deploy/k8s/secret.yaml` 即可。

## 4. 集群内 PostgreSQL（可选）

用 RDS 等云数据库时跳过本节。

```bash
kubectl -n yuncai create secret generic yuncai-db \
  --from-literal=POSTGRES_PASSWORD='<数据库口令>'
kubectl apply -f deploy/k8s/postgres/postgres.yaml
```

默认 20Gi PVC、库名/用户均为 `yuncai`，与第 3 步的连接串对应。

## 5. 部署

```bash
kubectl apply -k deploy/k8s
kubectl -n yuncai rollout status deploy/yuncai-api --timeout=10m
kubectl -n yuncai rollout status deploy/yuncai-web --timeout=5m
```

`api` 的 `initContainer` 会用同一镜像执行 `dotnet YunCai.dll --migrate-only`：先迁移、再启动主容器，所以不存在「新版本先于库结构启动」的窗口；`strategy: Recreate` 保证同一时刻只有一个实例在迁移。

## 6. 首次初始化管理员

`configmap.yaml` 当前是 `YunCai__Seed__Enabled: "true"`，首次 `kubectl apply -k` 会用 `yuncai-secret` 里的口令建出管理员与默认账套。种子跑完后把该文件改回 `"false"` 再 apply 一次——只改集群里的 ConfigMap 会被下一次 `apply -k` 覆盖回去。

种子幂等（用户表非空不会重复创建），但完成后要关掉，避免口令来源长期留在环境里。

## 7. 对外访问

域名只在 `domain.yaml` 一处改，Ingress 的 rule host 与 TLS hosts 由 kustomize `vars` 注入：

```yaml
data:
  domain: yuncai.datawalk.cn   # 当前配置；换域名只改这一行，不要带 https://
```

TLS 证书 Secret 名在 `ingress.yaml`（默认 `yuncai-tls`）。两者就绪后随 `kubectl apply -k deploy/k8s` 一起生效；只单独更新 Ingress 时：

```bash
kubectl kustomize deploy/k8s | kubectl apply -f -
```

不需要 Ingress 时，把 `kustomization.yaml` 里 `ingress.yaml` 从 `resources` 移除（`vars` 一并删掉），改由 NodePort/LoadBalancer 暴露 `web:80`。

`kubectl kustomize` 会打印一条 `'vars' is deprecated` 警告，属已知噪音：TLS 的 `hosts` 是纯字符串列表，`replacements` 定位不到元素，所以仍用 `vars`。

Ingress 只指向 `web:80`；`/api` 由 web 镜像里的 nginx 反代到 `api:8080`，仍保持「后端不直接对外」的约束——否则客户端可伪造 `X-Forwarded-For` 绕过登录限流。

## 8. 升级与回滚

```bash
# 改 kustomization.yaml 的 newTag 后
kubectl apply -k deploy/k8s
kubectl -n yuncai rollout status deploy/yuncai-api

# 回滚
kubectl -n yuncai rollout undo deploy/yuncai-api
kubectl -n yuncai rollout undo deploy/yuncai-web
```

镜像同时保留 `sha-<短哈希>` 标签，可回滚到任意一次构建。

## 9. 运行约束

- **api 默认单副本**：电子税务局登录会话在进程内存里（登录态另加密存库），多副本会让同一用户飘到没有会话的 Pod。要扩容先把会话做成共享存储或加会话亲和，并注意迁移不要多实例并发（可改为独立 Job）。
- **内存**：服务端登录税局要跑无头 Chromium，`limits` 给到 2Gi；`/dev/shm` 挂了 1Gi 的 `emptyDir`，否则 Chromium 会随机崩。
- **上传体积**：Ingress 与 web 的 nginx 都是 32MB，改大时两边一起改。
- **日志**：输出到 stdout（Serilog Console），用 `kubectl logs` 或集群日志采集，不要依赖文件。
- **备份**：备份 PostgreSQL 即可；税务登录态等都在库里。参考 `docs/部署上线说明.md` 的备份章节。

## 10. 排障

| 现象 | 排查 |
| --- | --- |
| `ImagePullBackOff` | `acr-pull` Secret 是否存在、registry 域名与 ACR 控制台一致 |
| Pod 卡在 `Init:0/1` | `kubectl -n yuncai logs <pod> -c migrate`：连接串、数据库可达性、口令 |
| 启动即退出 | 进程对缺失配置是设计性失败：`ConnectionStrings__DefaultConnection`、`Jwt__Key`（≥32 位）必须存在 |
| `/health` 不通 | `kubectl -n yuncai describe pod` 看探针；健康检查含数据库连通性 |
| 税局取数随机失败 | 内存不足或 `/dev/shm` 太小；看 api 日志里的 Chromium 崩溃 |
| ACR 登录 403 `Forbidden Host` | 该地域端点不属于此账号，换 ACR 控制台给出的地域域名 |
