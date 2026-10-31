/** 现金日记账：账页实现见 @/pages/funds/journal-ledger（与银行日记账共用）。 */
import { JournalLedgerPage } from "@/pages/funds/journal-ledger"

export default function CashJournalPage() {
    return <JournalLedgerPage journalType={1} seqPrefix="1-001" />
}
