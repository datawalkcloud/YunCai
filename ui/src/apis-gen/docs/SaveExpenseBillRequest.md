
# SaveExpenseBillRequest


## Properties

Name | Type
------------ | -------------
`billNo` | string
`billDate` | Date
`reason` | string
`expenseType` | string
`payee` | string
`settleMethod` | string
`amount` | number
`deductibleTax` | number

## Example

```typescript
import type { SaveExpenseBillRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "billNo": null,
  "billDate": null,
  "reason": null,
  "expenseType": null,
  "payee": null,
  "settleMethod": null,
  "amount": null,
  "deductibleTax": null,
} satisfies SaveExpenseBillRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveExpenseBillRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


