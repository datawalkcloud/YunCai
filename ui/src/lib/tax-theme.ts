/**
 * 智能报税工作台（pages/tax）共用的 antd 主题。
 *
 * 圆角不在这里覆写：全站基准由 main.tsx 的 antd token（borderRadius:6）与 index.css 的 --radius 控制，
 * 页面级 ConfigProvider 一旦写 borderRadius:2，整棵子树的按钮/输入框/表格都会被压成近直角。
 * 手写 Tailwind 容器同理，用 rounded-md（=6px）对齐，不要写 rounded-[2px]。
 */
import type { ThemeConfig } from "antd";

/** 税局品牌蓝，与 index.css 的 --primary/--ring 同值。 */
export const TAX_PRIMARY = "#2f6bff";

/** 税局风格的表头底色、表头文字、边框与悬停行底色。 */
export const TAX_TABLE_TOKEN = {
    headerBg: "#f4f7ff",
    headerColor: "#475569",
    borderColor: "#eef2f7",
    rowHoverBg: "#f6f9ff",
};

export const TAX_THEME: ThemeConfig = {
    token: { colorPrimary: TAX_PRIMARY },
    components: { Table: TAX_TABLE_TOKEN },
};

/** 在 TAX_THEME 基础上微调表格（如加宽单元格内边距）。 */
export function taxTheme(tableToken: Partial<typeof TAX_TABLE_TOKEN> & Record<string, number | string>): ThemeConfig {
    return {
        token: { colorPrimary: TAX_PRIMARY },
        components: { Table: { ...TAX_TABLE_TOKEN, ...tableToken } },
    };
}
