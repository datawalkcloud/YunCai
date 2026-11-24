/**
 * 站点运行时配置读取。
 *
 * 配置由 public/site.config.js 在 index.html 中以普通 <script> 同步注入到
 * window.__YUNCAI_SITE__，因此 React 挂载时已就绪，可直接同步读取，无异步闪烁。
 * 私有化部署改 site.config.js 即可，无需重新构建前端。
 */

export interface SiteConfig {
  /** 是否展示指向官方官网的入口（登录页品牌标识 + 返回官网）。缺省 false。 */
  showOfficialSite: boolean;
  /** 问题反馈入口：Gitee 新建 Issue 的完整地址，留空则隐藏该项。 */
  feedbackGiteeUrl: string;
  /** 问题反馈入口：GitHub 新建 Issue 的完整地址，留空则隐藏该项。 */
  feedbackGithubUrl: string;
}

declare global {
  interface Window {
    __YUNCAI_SITE__?: Partial<SiteConfig>;
  }
}

const DEFAULTS: SiteConfig = {
  showOfficialSite: false,
  feedbackGiteeUrl: "https://gitee.com/datawalkcloud/YunCai/issues/new",
  feedbackGithubUrl: "https://github.com/datawalkcloud/YunCai/issues/new",
};

/** 读取站点配置；未注入的字段回落到默认值（默认不展示官网入口，反馈入口指向官方仓库）。 */
export function getSiteConfig(): SiteConfig {
  const injected = typeof window !== "undefined" ? window.__YUNCAI_SITE__ : undefined;
  return {
    showOfficialSite: injected?.showOfficialSite ?? DEFAULTS.showOfficialSite,
    feedbackGiteeUrl: injected?.feedbackGiteeUrl ?? DEFAULTS.feedbackGiteeUrl,
    feedbackGithubUrl: injected?.feedbackGithubUrl ?? DEFAULTS.feedbackGithubUrl,
  };
}
