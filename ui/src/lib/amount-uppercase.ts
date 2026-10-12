/**
 * 人民币金额大写（公共工具）。
 * 由凭证录入页的合计显示与期末结转的凭证打印共用，避免多份实现漂移。
 */
export function amountToChineseUppercase(n: number): string {
    if (n === 0) return "零元整"
    const fraction = ["角", "分"]
    const digit = ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖"]
    const unit = [
        ["元", "万", "亿"],
        ["", "拾", "佰", "仟"],
    ]
    const head = n < 0 ? "负" : ""
    let num = Math.abs(n)
    let s = ""
    for (let i = 0; i < fraction.length; i++) {
        s += (digit[Math.floor(num * 10 * Math.pow(10, i)) % 10] + fraction[i]).replace(/零./, "")
    }
    s = s || "整"
    num = Math.floor(num)
    for (let i = 0; i < unit[0].length && num > 0; i++) {
        let p = ""
        for (let j = 0; j < unit[1].length && num > 0; j++) {
            p = digit[num % 10] + unit[1][j] + p
            num = Math.floor(num / 10)
        }
        s = p.replace(/(零.)*零$/, "").replace(/^$/, "零") + unit[0][i] + s
    }
    return head + s.replace(/(零.)*零元/, "元").replace(/(零.)*零$/, "").replace(/^整$/, "零元整")
}
