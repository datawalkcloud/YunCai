import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from "react"
import { useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"

import { useAuth } from "@/auth/AuthContext"
import { fetchMyPermissions } from "@/api/security"

/* ------------------------------------------------------------------ */
/*  账套内权限上下文                                                     */
/*                                                                      */
/*  后端为每个「模块.动作」权限键做强制校验；前端据此隐藏菜单与操作入口。    */
/*  账套管理员（角色 is_admin）拥有全部权限键；系统管理员若不在成员表里，   */
/*  这里拿不到权限，但自身仍可通过系统管理入口维护账号。                   */
/* ------------------------------------------------------------------ */

interface PermissionsValue {
    loading: boolean
    error: string | null
    roleName: string
    isAdmin: boolean
    permissionText: string
    /** 是否拥有某个权限键，如 vouchers.edit。 */
    has: (key: string) => boolean
    /** 模块是否至少有查看权限。 */
    canView: (module: string) => boolean
}

const EMPTY = new Set<string>()

/** 伪权限键：只对系统管理员开放的功能（如用户管理）。 */
export const SYSTEM_ADMIN_PERMISSION = "__sysadmin"

const PermissionsContext = createContext<PermissionsValue>({
    loading: false,
    error: null,
    roleName: "",
    isAdmin: false,
    permissionText: "",
    has: () => false,
    canView: () => false,
})

export function PermissionsProvider({ children }: { children: ReactNode }) {
    const accountSetId = useCurrentAccountSetId()
    const { token, user } = useAuth()
    const [keys, setKeys] = useState<Set<string>>(EMPTY)
    const [roleName, setRoleName] = useState("")
    const [isAdmin, setIsAdmin] = useState(false)
    const [permissionText, setPermissionText] = useState("")
    const [loading, setLoading] = useState(Boolean(accountSetId))
    const [error, setError] = useState<string | null>(null)

    useEffect(() => {
        if (!accountSetId || !token) {
            setKeys(EMPTY)
            setRoleName("")
            setIsAdmin(false)
            setPermissionText("")
            setLoading(false)
            setError(null)
            return
        }

        let cancelled = false
        setLoading(true)
        setError(null)
        fetchMyPermissions(accountSetId, token)
            .then((data) => {
                if (cancelled) return
                setKeys(new Set(data.permissions))
                setRoleName(data.roleName)
                setIsAdmin(data.isAdmin)
                setPermissionText(data.permissionText)
            })
            .catch((err) => {
                if (cancelled) return
                setKeys(EMPTY)
                setIsAdmin(false)
                setError(err instanceof Error ? err.message : "加载账套权限失败。")
            })
            .finally(() => {
                if (!cancelled) setLoading(false)
            })

        return () => {
            cancelled = true
        }
    }, [accountSetId, token])

    const value = useMemo<PermissionsValue>(
        () => ({
            loading,
            error,
            roleName,
            isAdmin,
            permissionText,
            has: (key) =>
                key === SYSTEM_ADMIN_PERMISSION ? user?.isSystemAdmin === true : isAdmin || keys.has(key),
            canView: (module) => isAdmin || keys.has(`${module}.view`),
        }),
        [loading, error, roleName, isAdmin, permissionText, keys, user?.isSystemAdmin],
    )

    return <PermissionsContext value={value}>{children}</PermissionsContext>
}

export function usePermissions(): PermissionsValue {
    return useContext(PermissionsContext)
}
