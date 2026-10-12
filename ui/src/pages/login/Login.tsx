import bannerFinanceImg from "@/assets/banner-finance-3d.png";

import { LoginForm } from "./components/login-form";

function Logo() {
  return (
    <svg viewBox="0 0 32 32" className="size-9 shrink-0" fill="none">
      <defs>
        <linearGradient id="loginLogo" x1="4" y1="2" x2="28" y2="30" gradientUnits="userSpaceOnUse">
          <stop stopColor="#3b82f6" />
          <stop offset="1" stopColor="#22c55e" />
        </linearGradient>
      </defs>
      <path d="M16 2 28 9v14l-12 7L4 23V9z" fill="url(#loginLogo)" />
      <path d="M10.5 16.5 14.5 20.5 22 11.5" stroke="#fff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" />
    </svg>
  );
}

export default function LoginPage() {
  return (
    <div className="grid min-h-svh lg:grid-cols-2">
      {/* 左侧：品牌 + 登录表单 */}
      <div className="flex flex-col p-8 md:p-12">
        <div className="flex items-center gap-2.5">
          <Logo />
          <div className="leading-tight">
            <div className="text-[17px] font-bold text-slate-800">云财</div>
            <div className="text-[11px] text-slate-400">让企业财务更简单</div>
          </div>
        </div>
        <div className="flex flex-1 items-center justify-center">
          <div className="w-full max-w-sm">
            <LoginForm />
          </div>
        </div>
        <div className="text-center text-[12px] text-slate-400">© 2026 云财 · 让企业财务更简单</div>
      </div>

      {/* 右侧：品牌介绍面板（呼应仪表盘欢迎横幅） */}
      <div className="relative hidden overflow-hidden bg-gradient-to-br from-[#e7f0ff] via-[#eef4ff] to-[#e4f7ec] lg:block">
        <div className="absolute -right-16 -top-16 size-64 rounded-full bg-[#2f6bff]/5" />
        <div className="absolute -bottom-20 -left-10 size-72 rounded-full bg-[#22c55e]/5" />
        <div className="absolute inset-0 flex flex-col items-center justify-center gap-8 p-12">
          <img src={bannerFinanceImg} alt="" className="h-[300px] w-auto drop-shadow-xl" />
          <div className="text-center">
            <h2 className="text-[28px] font-bold text-slate-800">财务，让经营更有底气</h2>
            <p className="mt-2 text-[14px] text-slate-500">账套 · 凭证 · 账簿 · 报表，一站式企业财务管理</p>
          </div>
        </div>
      </div>
    </div>
  );
}
