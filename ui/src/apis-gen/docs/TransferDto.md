
# TransferDto


## Properties

Name | Type
------------ | -------------
`id` | string
`date` | Date
`summary` | string
`fromJournalId` | string
`fromJournalName` | string
`toJournalId` | string
`toJournalName` | string
`amount` | number
`remark` | string
`voucherId` | string
`voucherNo` | string

## Example

```typescript
import type { TransferDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "date": null,
  "summary": null,
  "fromJournalId": null,
  "fromJournalName": null,
  "toJournalId": null,
  "toJournalName": null,
  "amount": null,
  "remark": null,
  "voucherId": null,
  "voucherNo": null,
} satisfies TransferDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TransferDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


