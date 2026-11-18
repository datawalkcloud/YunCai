
# GenerateExpenseVouchersRequest


## Properties

Name | Type
------------ | -------------
`billIds` | Array&lt;string&gt;
`expenseAccountId` | string
`counterAccountId` | string
`taxAccountId` | string
`word` | string
`preparer` | string

## Example

```typescript
import type { GenerateExpenseVouchersRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "billIds": null,
  "expenseAccountId": null,
  "counterAccountId": null,
  "taxAccountId": null,
  "word": null,
  "preparer": null,
} satisfies GenerateExpenseVouchersRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as GenerateExpenseVouchersRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


