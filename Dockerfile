# syntax=docker/dockerfile:1

# ---------- 前端构建 ----------
FROM node:24-alpine AS ui-build
WORKDIR /src/ui
COPY ui/package*.json ./
RUN npm ci
COPY ui/ ./
RUN npm run build

# ---------- 后端编译 ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
# 先只拷 csproj 做 restore，源码变更时能命中依赖层缓存
COPY src/YunCai/YunCai.csproj src/YunCai/
RUN dotnet restore src/YunCai/YunCai.csproj
COPY src/YunCai/ src/YunCai/
RUN dotnet publish src/YunCai/YunCai.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---------- 后端运行镜像（api 服务与一次性迁移任务共用）----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_EnableDiagnostics=0 \
    PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
COPY --from=api-build /app/publish ./
# 服务端登录电子税务局需要无头 Chromium；--with-deps 会一并装齐系统依赖。
RUN /app/.playwright/node/linux-x64/node /app/.playwright/package/cli.js install --with-deps chromium
EXPOSE 8080
ENTRYPOINT ["dotnet", "YunCai.dll"]

# ---------- 前端站点镜像（nginx 托管 SPA 并反代 /api）----------
FROM nginx:1.27-alpine AS web
COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=ui-build /src/ui/dist /usr/share/nginx/html
EXPOSE 80
