
# VoucherJournalDto


## Properties

Name | Type
------------ | -------------
`meta` | [LedgerMetaDto](LedgerMetaDto.md)
`rows` | [Array&lt;VoucherJournalRowDto&gt;](VoucherJournalRowDto.md)
`totalDebit` | number
`totalCredit` | number
`voucherCount` | number
`totalAttachments` | number

## Example

```typescript
import type { VoucherJournalDto } from ''

// TODO: Update the object below with actual values
const example = {
  "meta": null,
  "rows": null,
  "totalDebit": null,
  "totalCredit": null,
  "voucherCount": null,
  "totalAttachments": null,
} satisfies VoucherJournalDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherJournalDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


