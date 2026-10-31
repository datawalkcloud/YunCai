
# ExpenseBillDto


## Properties

Name | Type
------------ | -------------
`id` | string
`billNo` | string
`billDate` | Date
`reason` | string
`expenseType` | string
`payee` | string
`settleMethod` | string
`amount` | number
`deductibleTax` | number
`netAmount` | number
`statusText` | string
`status` | number
`voucherId` | string
`voucherText` | string

## Example

```typescript
import type { ExpenseBillDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "billNo": null,
  "billDate": null,
  "reason": null,
  "expenseType": null,
  "payee": null,
  "settleMethod": null,
  "amount": null,
  "deductibleTax": null,
  "netAmount": null,
  "statusText": null,
  "status": null,
  "voucherId": null,
  "voucherText": null,
} satisfies ExpenseBillDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ExpenseBillDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


