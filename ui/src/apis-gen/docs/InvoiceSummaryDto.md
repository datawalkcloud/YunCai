
# InvoiceSummaryDto


## Properties

Name | Type
------------ | -------------
`count` | number
`totalAmount` | number
`totalTax` | number
`totalWithTax` | number

## Example

```typescript
import type { InvoiceSummaryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "count": null,
  "totalAmount": null,
  "totalTax": null,
  "totalWithTax": null,
} satisfies InvoiceSummaryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as InvoiceSummaryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


