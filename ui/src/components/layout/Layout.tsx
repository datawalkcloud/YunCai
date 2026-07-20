import { useState } from "react"
import { Outlet } from "react-router"
import { MenuFoldOutlined, MenuUnfoldOutlined } from "@ant-design/icons"
import { Button, Layout as AntLayout, theme } from "antd"

import { AppSidebar } from "./components/app-sidebar"
import { ModeToggle } from "@/components/theme/mode-toggle"

const { Header, Sider, Content } = AntLayout

export default function Layout() {
  const [collapsed, setCollapsed] = useState(false)
  const { token } = theme.useToken()

  return (
    <AntLayout style={{ minHeight: "100svh" }}>
      <Sider
        collapsible
        collapsed={collapsed}
        trigger={null}
        width={240}
        collapsedWidth={72}
        style={{
          background: token.colorBgContainer,
          borderInlineEnd: `1px solid ${token.colorBorderSecondary}`,
          position: "sticky",
          top: 0,
          height: "100svh",
          overflow: "auto",
        }}
      >
        <AppSidebar collapsed={collapsed} />
      </Sider>
      <AntLayout style={{ display: "flex", flexDirection: "column", height: "100svh", overflow: "hidden" }}>
        <Header
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            height: 56,
            lineHeight: "56px",
            paddingInline: 16,
            background: token.colorBgContainer,
            borderBottom: `1px solid ${token.colorBorderSecondary}`,
            flexShrink: 0,
          }}
        >
          <div className="flex items-center gap-2">
            <Button
              type="text"
              onClick={() => setCollapsed((v) => !v)}
              icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
            />
            <span style={{ fontWeight: 500 }}>好会计</span>
          </div>
          <ModeToggle />
        </Header>
        <Content style={{ padding: 16, overflow: "auto" }}>
          <Outlet />
        </Content>
      </AntLayout>
    </AntLayout>
  )
}
