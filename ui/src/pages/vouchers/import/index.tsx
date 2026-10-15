import { useRef, useState } from "react"
import { Alert, Button } from "antd"
import { FileExcelOutlined, UploadOutlined } from "@ant-design/icons"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { VouchersApi, type VoucherImportResult } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"
import { SectionCard } from "@/components/page/section"
import { ICON_STYLE } from "@/components/page/styles"
import { Stat } from "@/components/stats/stat"

/** 允许的文件扩展名（与后端 NPOI 支持一致）。 */
const ACCEPT = ".xls,.xlsx"

export default function ImportVoucherPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const inputRef = useRef<HTMLInputElement>(null)
    const [file, setFile] = useState<File | null>(null)
    const [dragging, setDragging] = useState(false)
    const [importing, setImporting] = useState(false)
    const [result, setResult] = useState<VoucherImportResult | null>(null)
    const [error, setError] = useState<string | null>(null)

    // 目标账套由路由第一段决定（URL：/{accountSetId}/vouchers/import），切换请用左侧账套切换器。
    const currentSet = accountSets.find((s) => s.id === accountSetId)

    function pickFile(next: File | null) {
        setFile(next)
        setResult(null)
        setError(null)
    }

    function onDrop(e: React.DragEvent<HTMLDivElement>) {
        e.preventDefault()
        setDragging(false)
        const dropped = e.dataTransfer.files?.[0]
        if (dropped) pickFile(dropped)
    }

    async function handleImport() {
        if (!file || importing) return
        if (!accountSetId) {
            setError("请先选择要导入到的账套。")
            return
        }
        setImporting(true)
        setError(null)
        setResult(null)
        try {
            const res = await new VouchersApi(apiConfig(token)).accountsetsAccountSetIdVouchersImportPost({
                accountSetId,
                file,
            })
            setResult(res)
        } catch (err) {
            setError(await apiErrorMessage(err, "导入失败，请重试。"))
        } finally {
            setImporting(false)
        }
    }

    return (
        <div className="flex flex-col gap-6">
            <div className="flex flex-col gap-1">
                <h1 className="text-lg font-semibold">凭证导入</h1>
                <span className="text-muted-foreground">
                    支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                    借贷金额原样保留。
                </span>
            </div>

            <SectionCard bodyClassName="flex flex-col gap-4">
                {/* 目标账套：由路由第一段决定，切换请使用左侧账套切换器 */}
                <div className="flex flex-col gap-2">
                    <span className="font-medium">导入到账套</span>
                    {accountSetId ? (
                        <span className="text-muted-foreground">
                            {currentSet?.name ?? accountSetId}
                            {currentSet?.isCurrent ? "（当前）" : ""}
                        </span>
                    ) : (
                        <span className="text-destructive">未指定账套，请从左侧选择账套后再导入。</span>
                    )}
                </div>

                {/* 文件拖放 / 选择区 */}
                <div
                    role="button"
                    tabIndex={0}
                    onClick={() => inputRef.current?.click()}
                    onKeyDown={(e) => {
                        if (e.key === "Enter" || e.key === " ") inputRef.current?.click()
                    }}
                    onDragOver={(e) => {
                        e.preventDefault()
                        setDragging(true)
                    }}
                    onDragLeave={() => setDragging(false)}
                    onDrop={onDrop}
                    className={cn(
                        "flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors",
                        dragging ? "border-[#bfdbfe] bg-[#eef4ff]" : "border-border",
                    )}
                >
                    <FileExcelOutlined className="text-[40px]" style={ICON_STYLE.placeholder} />
                    {file ? (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">{file.name}</span>
                            <span className="text-sm leading-normal font-normal text-muted-foreground">
                                {(file.size / 1024).toFixed(1)} KB · 点击可重新选择
                            </span>
                        </div>
                    ) : (
                        <div className="flex flex-col gap-1">
                            <span className="font-medium">点击选择，或拖拽文件到此处</span>
                            <span className="text-sm leading-normal font-normal text-muted-foreground">
                                支持 .xls / .xlsx，最大 10MB
                            </span>
                        </div>
                    )}
                    <input
                        ref={inputRef}
                        type="file"
                        accept={ACCEPT}
                        className="hidden"
                        onChange={(e) => pickFile(e.target.files?.[0] ?? null)}
                    />
                </div>

                <div className="flex items-center gap-3">
                    <Button
                        type="primary"
                        loading={importing}
                        icon={importing ? undefined : <UploadOutlined />}
                        disabled={!file || !accountSetId}
                        onClick={() => void handleImport()}
                    >
                        {importing ? "导入中…" : "开始导入"}
                    </Button>
                    {file && !importing && <Button onClick={() => pickFile(null)}>清除</Button>}
                </div>

                {error && <Alert type="error" showIcon title={error} />}
            </SectionCard>

            {/* 导入结果 */}
            {result && (
                <SectionCard title={<h3 className="text-[14px] font-semibold text-slate-800">导入结果</h3>} bodyClassName="flex flex-col gap-4">
                    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
                        <Stat label="解析行数" value={result.totalRows ?? 0} />
                        <Stat label="导入凭证" value={result.importedVouchers ?? 0} />
                        <Stat label="导入分录" value={result.importedEntries ?? 0} />
                        <Stat label="新建科目" value={result.createdAccounts ?? 0} />
                        <Stat label="跳过凭证" value={result.skippedVouchers ?? 0} muted />
                    </div>

                    {result.warnings && result.warnings.length > 0 && (
                        <div className="flex flex-col gap-2">
                            <span className="text-muted-foreground">告警（{result.warnings.length}）</span>
                            <ul className="max-h-64 overflow-auto rounded-lg border border-border bg-muted p-3 text-sm">
                                {result.warnings.map((w, i) => (
                                    <li key={i} className="py-0.5 text-muted-foreground">
                                        · {w}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </SectionCard>
            )}
        </div>
    )
}
