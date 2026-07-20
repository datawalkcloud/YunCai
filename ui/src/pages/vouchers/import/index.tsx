import { useRef, useState } from "react"
import { FileSpreadsheet, Upload } from "lucide-react"
import { Alert, Button, Card, Col, Row, Statistic, Typography, theme } from "antd"

import { useAccountSets, useCurrentAccountSetId } from "@/accountSets/AccountSetsContext"
import { useAuth } from "@/auth/AuthContext"
import { VouchersApi, type VoucherImportResult } from "@/apis-gen"
import { apiConfig, apiErrorMessage } from "@/lib/api-client"
import { cn } from "@/lib/utils"

const { Title, Text } = Typography

/** 允许的文件扩展名（与后端 NPOI 支持一致）。 */
const ACCEPT = ".xls,.xlsx"

export default function ImportVoucherPage() {
    const { token } = useAuth()
    const { accountSets } = useAccountSets()
    const accountSetId = useCurrentAccountSetId() ?? ""
    const { token: t } = theme.useToken()
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
                <Title level={3} style={{ margin: 0 }}>凭证导入</Title>
                <Text type="secondary">
                    支持导入其他平台导出的记账凭证文件（.xls / .xlsx）。系统按凭证号聚合分录、自动补建缺失科目，
                    借贷金额原样保留。
                </Text>
            </div>

            <Card>
                <div className="flex flex-col gap-4">
                    {/* 目标账套：由路由第一段决定，切换请使用左侧账套切换器 */}
                    <div className="flex flex-col gap-2">
                        <Text strong>导入到账套</Text>
                        {accountSetId ? (
                            <Text type="secondary">
                                {currentSet?.name ?? accountSetId}
                                {currentSet?.isCurrent ? "（当前）" : ""}
                            </Text>
                        ) : (
                            <Text type="danger">未指定账套，请从左侧选择账套后再导入。</Text>
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
                        style={{
                            borderColor: dragging ? t.colorPrimary : t.colorBorder,
                            background: dragging ? t.colorPrimaryBg : undefined,
                        }}
                        className="flex cursor-pointer flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed p-10 text-center transition-colors"
                    >
                        <FileSpreadsheet size={40} color={t.colorTextSecondary} />
                        {file ? (
                            <div className="flex flex-col gap-1">
                                <Text strong>{file.name}</Text>
                                <Text type="secondary" style={{ fontSize: 12 }}>
                                    {(file.size / 1024).toFixed(1)} KB · 点击可重新选择
                                </Text>
                            </div>
                        ) : (
                            <div className="flex flex-col gap-1">
                                <Text strong>点击选择，或拖拽文件到此处</Text>
                                <Text type="secondary" style={{ fontSize: 12 }}>支持 .xls / .xlsx，最大 10MB</Text>
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
                            icon={<Upload size={16} />}
                            loading={importing}
                            onClick={handleImport}
                            disabled={!file || !accountSetId}
                        >
                            {importing ? "导入中…" : "开始导入"}
                        </Button>
                        {file && !importing && <Button onClick={() => pickFile(null)}>清除</Button>}
                    </div>

                    {error && <Alert type="error" showIcon message={error} />}
                </div>
            </Card>

            {/* 导入结果 */}
            {result && (
                <Card title="导入结果">
                    <Row gutter={[16, 16]}>
                        <Stat label="解析行数" value={result.totalRows ?? 0} />
                        <Stat label="导入凭证" value={result.importedVouchers ?? 0} />
                        <Stat label="导入分录" value={result.importedEntries ?? 0} />
                        <Stat label="新建科目" value={result.createdAccounts ?? 0} />
                        <Stat label="跳过凭证" value={result.skippedVouchers ?? 0} muted />
                    </Row>

                    {result.warnings && result.warnings.length > 0 && (
                        <div className="mt-4 flex flex-col gap-2">
                            <Text type="secondary">告警（{result.warnings.length}）</Text>
                            <ul
                                className="max-h-64 overflow-auto rounded-md p-3 text-sm"
                                style={{ border: `1px solid ${t.colorBorderSecondary}`, background: t.colorFillQuaternary }}
                            >
                                {result.warnings.map((w, i) => (
                                    <li key={i} className={cn("py-0.5")} style={{ color: t.colorTextSecondary }}>
                                        · {w}
                                    </li>
                                ))}
                            </ul>
                        </div>
                    )}
                </Card>
            )}
        </div>
    )
}

function Stat({ label, value, muted }: { label: string; value: number; muted?: boolean }) {
    return (
        <Col xs={12} sm={8} lg={muted ? 24 : 4} flex="auto">
            <Card size="small">
                <Statistic
                    title={label}
                    value={value}
                    valueStyle={muted ? { color: "rgba(0,0,0,0.45)" } : undefined}
                />
            </Card>
        </Col>
    )
}
