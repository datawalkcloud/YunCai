import { BugOutlined, GithubOutlined } from "@ant-design/icons"
import type { MenuProps } from "antd"
import { Button, Dropdown, Tooltip } from "antd"

import { getSiteConfig } from "@/lib/site-config"

/** Gitee 官方标志（simple-icons 路径），antd 图标库里没有 Gitee。 */
function GiteeIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            width="1em"
            height="1em"
            fill="#C71D23"
            aria-hidden
            focusable="false"
            // 原生 svg 不带 anticon 类，没有 antd 菜单图标的 8px 尾距，这里补齐与 GitHubOutlined 一致。
            style={{ marginInlineEnd: 8 }}
        >
            <path d="M11.984 0A12 12 0 0 0 0 12a12 12 0 0 0 12 12 12 12 0 0 0 12-12A12 12 0 0 0 12 0a12 12 0 0 0-.016 0zm6.09 5.333c.328 0 .593.266.592.593v1.482a.594.594 0 0 1-.593.592H9.777c-.982 0-1.778.796-1.778 1.778v5.63c0 .327.266.592.593.592h5.63c.982 0 1.778-.796 1.778-1.778v-.296a.593.593 0 0 0-.592-.593h-4.15a.592.592 0 0 1-.592-.592v-1.482a.593.593 0 0 1 .593-.592h6.815c.327 0 .593.265.593.592v3.408a4 4 0 0 1-4 4H5.926a.593.593 0 0 1-.593-.593V9.778a4.444 4.444 0 0 1 4.445-4.444h8.296Z" />
        </svg>
    )
}

/**
 * 顶栏问题反馈入口：点击后选择 Gitee / GitHub，在新标签页打开对应仓库的新建 Issue 页。
 * 地址来自站点运行时配置；私有化部署可改成自己的仓库，或置空隐藏对应项（两项皆空则隐藏整个入口）。
 */
export function FeedbackMenu() {
    const config = getSiteConfig()
    const giteeUrl = config.feedbackGiteeUrl.trim()
    const githubUrl = config.feedbackGithubUrl.trim()

    const items: MenuProps["items"] = [
        ...(giteeUrl
            ? [
                  {
                      key: "gitee",
                      icon: <GiteeIcon />,
                      label: (
                          <a href={giteeUrl} target="_blank" rel="noopener noreferrer">
                              Gitee 反馈
                          </a>
                      ),
                  },
              ]
            : []),
        ...(githubUrl
            ? [
                  {
                      key: "github",
                      icon: <GithubOutlined />,
                      label: (
                          <a href={githubUrl} target="_blank" rel="noopener noreferrer">
                              GitHub 反馈
                          </a>
                      ),
                  },
              ]
            : []),
    ]

    if (items.length === 0) return null

    return (
        <Dropdown trigger={["click"]} placement="bottomRight" menu={{ items }}>
            <Tooltip title="问题反馈" placement="bottom">
                <Button type="text" icon={<BugOutlined />} aria-label="问题反馈" />
            </Tooltip>
        </Dropdown>
    )
}
