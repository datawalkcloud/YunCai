/**
 * 人民币金额大写（公共工具）。
 * 由凭证录入页的合计显示、凭证打印与期末结转共用，避免多份实现漂移。
 *
 * 算法与后端 `ChineseAmount.ToUppercase`（src/KuaiJi/Shared/Infrastructure/ChineseAmount.cs）严格一致，
 * 保证浏览器打印预览与服务端 PDF 输出的大写金额完全相同。
 * 规则参照中国人民银行《正确填写票据和结算凭证的基本规定》。
 */
const DIGITS = ["零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖"]
const UNITS = ["", "拾", "佰", "仟"]
const SECTIONS = ["", "万", "亿", "万亿"]

/** 整数部分转中文大写（四位分节）。 */
function integerToChinese(value: number): string {
    if (value <= 0) return ""
    const text = String(Math.floor(value))
    let sb = ""
    let pendingZero = false

    for (let i = 0; i < text.length; i++) {
        const digit = text.charCodeAt(i) - 48
        const pos = text.length - 1 - i
        const unitIndex = pos % 4
        const sectionIndex = Math.floor(pos / 4)

        if (digit !== 0) {
            if (pendingZero && sb.length > 0) sb += "零"
            pendingZero = false
            sb += DIGITS[digit] + UNITS[unitIndex]
        } else {
            // 只有后面还会出现非零数字时，这个零才有意义
            pendingZero = sb.length > 0
        }

        // 到达某一节的个位时补节名（万 / 亿），该节全为零时不补
        if (unitIndex === 0 && sectionIndex > 0) {
            let hasValue = false
            for (let k = Math.max(0, i - 3); k <= i; k++) {
                if (text.charCodeAt(k) !== 48) {
                    hasValue = true
                    break
                }
            }
            if (hasValue) sb += SECTIONS[sectionIndex]
            else if (sb.length > 0) pendingZero = true
        }
    }

    return sb
}

export function amountToChineseUppercase(n: number): string {
    // 先折算成整数「分」再运算，避免 1234.56 * 100 之类的浮点误差
    const cents = Math.round(Math.abs(n) * 100)
    let yuan = Math.floor(cents / 100)
    let fen = cents % 100
    if (fen >= 100) {
        yuan += 1
        fen = 0
    }

    // 零元零分单独成文
    if (yuan === 0 && fen === 0) return "零元整"

    let s = n < 0 ? "负" : ""

    // 元位为零时只写角分（壹角 / 壹分），不额外带出「零元」
    const hasYuan = yuan > 0
    if (hasYuan) s += integerToChinese(yuan) + "元"

    if (fen === 0) return s + "整"

    const jiao = Math.floor(fen / 10)
    const cent = fen % 10

    // 角位为零而分位不为零时补「零」：壹元零伍分
    if (jiao === 0 && cent > 0 && hasYuan) s += "零"
    if (jiao > 0) s += DIGITS[jiao] + "角"
    if (cent > 0) s += DIGITS[cent] + "分"

    return s
}
