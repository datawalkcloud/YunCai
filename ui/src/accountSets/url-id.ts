/**
 * 账套 id 的两种写法：接口与领域对象一律用标准 GUID（带横杠，36 位），
 * 浏览器地址栏只展示去掉横杠的紧凑形态（32 位十六进制）。
 * 两者是同一个 id，转换收口在这里：写路由用 setPath，读路由用 AccountSetsContext 的 useCurrentAccountSetId，
 * 页面不要自己拼 `/${id}` 或 replace("-")。
 */

const HEX_32 = /^[0-9a-fA-F]{32}$/

/** 地址栏用：去掉 GUID 横杠；已经是紧凑形态时原样返回。只在 setPath 内部调用。 */
function toUrlId(id: string): string {
    return id.replaceAll("-", "")
}

/** 账套内路由：紧凑 id 打头，其后原样接业务路径（含 query）。页面只写业务路径，不关心 id 形态。 */
export function setPath(id: string | undefined, rest = ""): string {
    return `/${toUrlId(id ?? "")}${rest}`
}

/**
 * 从地址栏读回：32 位十六进制补回横杠，其余（含历史带横杠链接）原样接受，
 * 这样内部比较与接口请求看到的永远是标准 GUID。
 */
export function fromUrlId(raw: string | undefined | null): string | undefined {
    if (!raw) return undefined
    return HEX_32.test(raw)
        ? `${raw.slice(0, 8)}-${raw.slice(8, 12)}-${raw.slice(12, 16)}-${raw.slice(16, 20)}-${raw.slice(20)}`
        : raw
}
