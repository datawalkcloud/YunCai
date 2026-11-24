/**
 * 站点运行时配置（私有化 / 白标部署用）
 *
 * 本文件位于 public/，构建时会被原样拷到 dist/ 根目录，作为【非 Docker 静态部署】的默认值。
 *
 * Docker 部署（推荐）：容器启动时由 /docker-entrypoint.d/10-site-config.sh 依据环境变量
 * YUNCAI_SHOW_OFFICIAL_SITE 重新生成本文件，覆盖下面的默认值——默认不展示官网入口，
 * 需要展示时在 .env 设 YUNCAI_SHOW_OFFICIAL_SITE=true 并重启 web 容器，无需进容器改文件、
 * 无需重新构建镜像。
 *
 * showOfficialSite：是否展示指向官方营销官网的入口（登录页顶部的品牌标识 +「返回官网」）。
 *   - 默认 false：不展示任何指向官方官网的入口（适合私有化 / 二次分发）。
 *   - 需要展示时设 true：登录页顶部出现品牌标识与「返回官网」。
 *
 * feedbackGiteeUrl / feedbackGithubUrl：顶栏「问题反馈」里两个平台的新建 Issue 地址。
 *   - 默认指向官方仓库；私有化部署可改成自己的地址，或设为空字符串隐藏对应项。
 *   - 两项都为空时，顶栏不再显示问题反馈入口。
 *
 * 注意：官网首页本身由 nginx 的 `location = / -> home.html` 返回。若私有化部署
 * 需要连根路径的官方营销页也一并去掉，需另外调整 ui/nginx.conf（本开关只管入口）。
 */
window.__YUNCAI_SITE__ = {
  showOfficialSite: false,
  feedbackGiteeUrl: "https://gitee.com/datawalkcloud/YunCai/issues/new",
  feedbackGithubUrl: "https://github.com/datawalkcloud/YunCai/issues/new",
};
