import { Link, useParams, useSearchParams } from "react-router";
import { LeftOutlined, ToolOutlined } from "@ant-design/icons";

/** 未实现菜单的统一占位页：展示「开发中」。 */
export default function ComingSoon() {
    const { accountSetId = "" } = useParams();
    const [searchParams] = useSearchParams();
    const menu = searchParams.get("menu") || "该功能";

    return (
        <div className="flex min-h-[60vh] items-center justify-center">
            <div className="flex w-full max-w-md flex-col items-center rounded-2xl border border-slate-100 bg-white px-8 py-12 text-center shadow-[0_1px_2px_rgba(16,24,40,0.04)]">
                <span className="flex size-16 items-center justify-center rounded-2xl bg-[#e7f0ff] text-[#2f6bff]">
                    <ToolOutlined className="text-[32px]" />
                </span>
                <h1 className="mt-5 text-[18px] font-semibold text-slate-800">「{menu}」功能开发中</h1>
                <p className="mt-2 text-[13px] leading-relaxed text-slate-500">
                    该模块正在紧锣密鼓地建设中，敬请期待后续版本更新。
                </p>
                <Link
                    to={`/${accountSetId}`}
                    className="mt-6 flex items-center gap-1.5 rounded-lg bg-[#2f6bff] px-4 py-2 text-[13px] font-medium text-white shadow-[0_1px_2px_rgba(47,107,255,0.25)] transition-colors hover:bg-[#255ce0]"
                >
                    <LeftOutlined className="text-[16px]" />
                    返回首页
                </Link>
            </div>
        </div>
    );
}
