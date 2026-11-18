
# InvoiceLineDto


## Properties

Name | Type
------------ | -------------
`id` | string
`lineNumber` | number
`goodsName` | string
`amount` | number
`taxRate` | number
`taxAmount` | number
`totalWithTax` | number

## Example

```typescript
import type { InvoiceLineDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "lineNumber": null,
  "goodsName": null,
  "amount": null,
  "taxRate": null,
  "taxAmount": null,
  "totalWithTax": null,
} satisfies InvoiceLineDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as InvoiceLineDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


