import { useAuth } from "@/auth/AuthContext"

export default function Home() {
    const { user } = useAuth()

    return (
        <div className="flex flex-col gap-2">
            <h1 className="text-2xl font-bold">欢迎，{user?.displayName ?? "用户"}</h1>
            <p className="text-muted-foreground">你已成功登录好会计。</p>
        </div>
    )
}
