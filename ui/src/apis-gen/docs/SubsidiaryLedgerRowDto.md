
# SubsidiaryLedgerRowDto


## Properties

Name | Type
------------ | -------------
`kind` | number
`date` | string
`voucherNo` | string
`summary` | string
`counterAccount` | string
`debit` | number
`credit` | number
`direction` | string
`balance` | number

## Example

```typescript
import type { SubsidiaryLedgerRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "kind": null,
  "date": null,
  "voucherNo": null,
  "summary": null,
  "counterAccount": null,
  "debit": null,
  "credit": null,
  "direction": null,
  "balance": null,
} satisfies SubsidiaryLedgerRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SubsidiaryLedgerRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


