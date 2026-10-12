
# InvoiceFundOverviewRowDto


## Properties

Name | Type
------------ | -------------
`partnerName` | string
`salesWithTax` | number
`received` | number
`notInvoiced` | number
`notReceived` | number

## Example

```typescript
import type { InvoiceFundOverviewRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "partnerName": null,
  "salesWithTax": null,
  "received": null,
  "notInvoiced": null,
  "notReceived": null,
} satisfies InvoiceFundOverviewRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as InvoiceFundOverviewRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


