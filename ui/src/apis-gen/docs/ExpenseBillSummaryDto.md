
# ExpenseBillSummaryDto


## Properties

Name | Type
------------ | -------------
`count` | number
`totalAmount` | number
`totalDeductibleTax` | number

## Example

```typescript
import type { ExpenseBillSummaryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "count": null,
  "totalAmount": null,
  "totalDeductibleTax": null,
} satisfies ExpenseBillSummaryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ExpenseBillSummaryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


