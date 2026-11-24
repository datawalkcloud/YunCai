#!/bin/sh
# 容器启动时按环境变量生成站点运行时配置，覆盖构建时 dist 里的默认值。
# 放在 /docker-entrypoint.d/ 下，nginx 官方镜像启动时会按序执行本脚本，再启动 nginx。
# 默认不展示官网入口；需要展示时在 .env / compose 里设 YUNCAI_SHOW_OFFICIAL_SITE=true。
# 问题反馈地址由 YUNCAI_FEEDBACK_GITEE_URL / YUNCAI_FEEDBACK_GITHUB_URL 覆盖，设为空串可隐藏。
# 以上都无需改镜像内的文件、无需重新构建前端。

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

# 问题反馈入口：默认指向官方仓库；私有化部署可改成自己的地址，设为空串则隐藏对应项。
# 写进 JS 字符串前先转义反斜杠与双引号。
_js_quote() {
  printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g' | tr '\n' ' '
}
# 注意用 ${VAR-default} 而非 ${VAR:-default}：显式空串是「隐藏该入口」，不能回落到默认值。
gitee_url=$(_js_quote "${YUNCAI_FEEDBACK_GITEE_URL-https://gitee.com/datawalkcloud/YunCai/issues/new}")
github_url=$(_js_quote "${YUNCAI_FEEDBACK_GITHUB_URL-https://github.com/datawalkcloud/YunCai/issues/new}")

cat > "$OUT" <<EOF
// 由容器启动时根据站点环境变量生成，请勿手改。
window.__YUNCAI_SITE__ = {
  showOfficialSite: $show,
  feedbackGiteeUrl: "$gitee_url",
  feedbackGithubUrl: "$github_url",
};
EOF

echo "[site-config] showOfficialSite=$show (raw='$_raw') feedbackGiteeUrl='$gitee_url' feedbackGithubUrl='$github_url' -> $OUT"
