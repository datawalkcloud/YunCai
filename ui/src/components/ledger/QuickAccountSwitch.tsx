import { useEffect, useMemo, useState } from "react"
import { SearchOutlined } from "@ant-design/icons"
import { Input, Tree } from "antd"
import type { TreeDataNode } from "antd"

import type { AccountDto } from "@/apis-gen"
import { cn } from "@/lib/utils"

interface AccountNode {
    key: string
    name: string
    children: AccountNode[]
}

/** 按 parentId 把扁平科目列表还原成树，同级按编码排序。 */
function buildTree(accounts: AccountDto[]): AccountNode[] {
    const nodes = new Map<string, AccountNode>()
    for (const account of accounts) {
        if (!account.code) continue
        nodes.set(account.id ?? account.code, {
            key: account.code,
            name: account.name ?? "",
            children: [],
        })
    }

    const roots: AccountNode[] = []
    for (const account of accounts) {
        const node = nodes.get(account.id ?? account.code ?? "")
        if (!node) continue
        const parent = account.parentId ? nodes.get(account.parentId) : undefined
        if (parent) parent.children.push(node)
        else roots.push(node)
    }

    const sort = (list: AccountNode[]) => {
        list.sort((a, b) => a.key.localeCompare(b.key))
        for (const item of list) sort(item.children)
    }
    sort(roots)
    return roots
}

/** 关键字命中自身或任一后代即保留；命中父级时整棵子树一并保留。 */
function filterTree(nodes: AccountNode[], keyword: string): AccountNode[] {
    const hit = (node: AccountNode): AccountNode | null => {
        if (node.key.includes(keyword) || node.name.includes(keyword)) return node
        const children = node.children.map(hit).filter((child): child is AccountNode => child !== null)
        return children.length > 0 ? { ...node, children } : null
    }
    return nodes.map(hit).filter((node): node is AccountNode => node !== null)
}

function collectKeys(nodes: AccountNode[], keys: string[] = []): string[] {
    for (const node of nodes) {
        if (node.children.length > 0) {
            keys.push(node.key)
            collectKeys(node.children, keys)
        }
    }
    return keys
}

/** 从根到指定编码的祖先链路，用于展开到当前科目。 */
function ancestorKeys(nodes: AccountNode[], code: string, trail: string[] = []): string[] {
    for (const node of nodes) {
        if (node.key === code) return trail
        const found = ancestorKeys(node.children, code, [...trail, node.key])
        if (found.length > 0) return found
    }
    return []
}

function toTreeData(nodes: AccountNode[]): TreeDataNode[] {
    return nodes.map((node) => ({
        key: node.key,
        title: (
            <span className="flex min-w-0 items-center gap-1.5 text-[12.5px]">
                <span className="shrink-0 tabular-nums text-(--muted-foreground)">{node.key}</span>
                <span className="truncate">{node.name}</span>
            </span>
        ),
        children: node.children.length > 0 ? toTreeData(node.children) : undefined,
    }))
}

export interface QuickAccountSwitchProps {
    accounts: AccountDto[]
    /** 当前定位的科目编码；空表示未选。 */
    value?: string
    onChange: (code: string) => void
    disabled?: boolean
    className?: string
}

/**
 * 明细账右侧「快速切换」：按科目树定位单个科目，点击即按其编码重新查询。
 */
export function QuickAccountSwitch({ accounts, value, onChange, disabled, className }: QuickAccountSwitchProps) {
    const [keyword, setKeyword] = useState("")
    const tree = useMemo(() => buildTree(accounts), [accounts])
    const filtered = useMemo(() => (keyword ? filterTree(tree, keyword) : tree), [tree, keyword])
    const [expandedKeys, setExpandedKeys] = useState<string[]>(() => (value ? ancestorKeys(tree, value) : []))

    // 搜索时展开全部命中；清空搜索后展开到当前科目。
    useEffect(() => {
        if (keyword) setExpandedKeys(collectKeys(filtered))
        else setExpandedKeys(value ? ancestorKeys(tree, value) : [])
    }, [keyword, filtered, tree, value])

    const data = useMemo(() => toTreeData(filtered), [filtered])

    return (
        <div className={cn("flex h-full flex-col border border-border bg-card", className)}>
            <div className="border-b border-border px-3 py-2 text-[13px] font-medium text-(--foreground)">快速切换</div>
            <div className="p-2">
                <Input
                    size="small"
                    allowClear
                    prefix={<SearchOutlined className="text-(--muted-foreground)" />}
                    placeholder="搜索科目编码/名称"
                    value={keyword}
                    disabled={disabled}
                    onChange={(e) => setKeyword(e.target.value.trim())}
                />
            </div>
            <div className="min-h-0 flex-1 overflow-y-auto px-1 pb-2">
                <Tree
                    blockNode
                    treeData={data}
                    disabled={disabled}
                    selectedKeys={value ? [value] : []}
                    expandedKeys={expandedKeys}
                    onExpand={(keys) => setExpandedKeys(keys as string[])}
                    onSelect={(_, info) => onChange(String(info.node.key))}
                />
            </div>
        </div>
    )
}
