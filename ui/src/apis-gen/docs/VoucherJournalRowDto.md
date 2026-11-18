
# VoucherJournalRowDto


## Properties

Name | Type
------------ | -------------
`date` | string
`voucherNo` | string
`summary` | string
`accountCode` | string
`accountName` | string
`debit` | number
`credit` | number
`preparer` | string
`reviewer` | string
`attachmentCount` | number
`remark` | string

## Example

```typescript
import type { VoucherJournalRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "voucherNo": null,
  "summary": null,
  "accountCode": null,
  "accountName": null,
  "debit": null,
  "credit": null,
  "preparer": null,
  "reviewer": null,
  "attachmentCount": null,
  "remark": null,
} satisfies VoucherJournalRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherJournalRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


