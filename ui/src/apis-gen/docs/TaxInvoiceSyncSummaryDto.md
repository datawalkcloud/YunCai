
# TaxInvoiceSyncSummaryDto


## Properties

Name | Type
------------ | -------------
`direction` | string
`periodStart` | Date
`periodEnd` | Date
`count` | number
`amount` | number
`tax` | number
`total` | number

## Example

```typescript
import type { TaxInvoiceSyncSummaryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "direction": null,
  "periodStart": null,
  "periodEnd": null,
  "count": null,
  "amount": null,
  "tax": null,
  "total": null,
} satisfies TaxInvoiceSyncSummaryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxInvoiceSyncSummaryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


