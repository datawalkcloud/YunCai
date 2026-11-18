
# JournalEntryDto


## Properties

Name | Type
------------ | -------------
`id` | string
`sequence` | number
`date` | Date
`summary` | string
`category` | string
`counterparty` | string
`project` | string
`department` | string
`income` | number
`expense` | number
`balance` | number
`voucherId` | string
`voucherNo` | string
`voucherDate` | Date
`voucherWord` | string
`voucherNumber` | number
`settlementMethod` | string
`billNo` | string
`remark` | string
`source` | string

## Example

```typescript
import type { JournalEntryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "sequence": null,
  "date": null,
  "summary": null,
  "category": null,
  "counterparty": null,
  "project": null,
  "department": null,
  "income": null,
  "expense": null,
  "balance": null,
  "voucherId": null,
  "voucherNo": null,
  "voucherDate": null,
  "voucherWord": null,
  "voucherNumber": null,
  "settlementMethod": null,
  "billNo": null,
  "remark": null,
  "source": null,
} satisfies JournalEntryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as JournalEntryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


