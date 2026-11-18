#!/bin/sh
# 容器启动时按环境变量生成站点运行时配置，覆盖构建时 dist 里的默认值。
# 放在 /docker-entrypoint.d/ 下，nginx 官方镜像启动时会按序执行本脚本，再启动 nginx。
# 默认不展示官网入口；需要展示时在 .env / compose 里设 YUNCAI_SHOW_OFFICIAL_SITE=true，
# 无需改镜像内的文件、无需重新构建前端。

set -e

OUT="${SITE_CONFIG_PATH:-/usr/share/nginx/html/site.config.js}"

# 只把明确的 true/1/yes/on（忽略大小写、去空格）视为开启，其余一律保持关闭。
# 缺省 false，私有化 / 二次分发默认不展示任何官方官网入口。
_raw="${YUNCAI_SHOW_OFFICIAL_SITE:-false}"
_val=$(printf '%s' "$_raw" | tr '[:upper:]' '[:lower:]' | tr -d '[:space:]')
case "$_val" in
  true|1|yes|on) show=true ;;
  *) show=false ;;
esac

cat > "$OUT" <<EOF
// 由容器启动时根据 YUNCAI_SHOW_OFFICIAL_SITE 生成，请勿手改。
window.__YUNCAI_SITE__ = { showOfficialSite: $show };
EOF

echo "[site-config] showOfficialSite=$show (raw='$_raw') -> $OUT"
