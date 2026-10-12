
# SaveInvoiceLineRequest


## Properties

Name | Type
------------ | -------------
`goodsName` | string
`amount` | number
`taxRate` | number
`taxAmount` | number

## Example

```typescript
import type { SaveInvoiceLineRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "goodsName": null,
  "amount": null,
  "taxRate": null,
  "taxAmount": null,
} satisfies SaveInvoiceLineRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveInvoiceLineRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


