import type { CSSProperties } from "react";

import { cn } from "@/lib/utils";

/**
 * 云财品牌记号：开口环 C（Cloud / 「财」首音）+ 缺口一枚圆点，单色几何。
 * fill=currentColor，颜色由父级 color 控制；viewBox 内容落在 ~5..27 安全区，
 * 直接按容器尺寸铺满即可（内边距已画进 viewBox）。
 */
export function BrandMark({ className, style }: { className?: string; style?: CSSProperties }) {
    return (
        <svg viewBox="0 0 32 32" className={className} style={style} fill="none" aria-hidden="true">
            <path
                d="M23.78 8.22A11 11 0 1 0 23.78 23.78L20.6 20.6A6.5 6.5 0 1 1 20.6 11.4Z"
                fill="currentColor"
            />
            <circle cx="23.1" cy="16" r="2.25" fill="currentColor" />
        </svg>
    );
}

/**
 * 品牌锁定：默认「方块」——纯平品牌底 + 白色 C 记号，底色走 --brand-fill，
 * 跟随「主题设置」的主题色与明暗；variant="bare" 只出裸标（品牌色，无底色）。
 * radius 缺省按尺寸取 ~0.265 的圆角，和参考候选一致。
 */
export function BrandLogo({
    size = 34,
    variant = "tile",
    radius,
    className,
}: {
    size?: number;
    variant?: "tile" | "bare";
    radius?: number;
    className?: string;
}) {
    if (variant === "bare") {
        return <BrandMark className={className} style={{ width: size, height: size, color: "var(--brand)" }} />;
    }
    return (
        <span
            className={cn("flex shrink-0 items-center justify-center", className)}
            style={{
                width: size,
                height: size,
                borderRadius: radius ?? Math.round(size * 0.265),
                background: "var(--brand-fill)",
            }}
        >
            <BrandMark style={{ width: size, height: size, color: "#fff" }} />
        </span>
    );
}