
# CashJournalRowDto


## Properties

Name | Type
------------ | -------------
`date` | string
`voucherNo` | string
`summary` | string
`contraAccount` | string
`income` | number
`expense` | number

## Example

```typescript
import type { CashJournalRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "voucherNo": null,
  "summary": null,
  "contraAccount": null,
  "income": null,
  "expense": null,
} satisfies CashJournalRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashJournalRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


