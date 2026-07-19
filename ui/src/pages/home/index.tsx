import { useNavigate } from "react-router"

import { Button } from "@/components/ui/button"

export default function Home() {
    const navigate = useNavigate()

    return (
        <div className="flex min-h-svh flex-col items-center justify-center gap-4">
            <h1 className="text-2xl font-bold">Home</h1>
            <Button onClick={() => navigate("/login")}>去登录</Button>
        </div>
    )
}
