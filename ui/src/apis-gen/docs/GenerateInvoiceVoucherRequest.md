
# GenerateInvoiceVoucherRequest


## Properties

Name | Type
------------ | -------------
`counterAccountId` | string
`subjectAccountId` | string
`taxAccountId` | string
`word` | string
`date` | Date

## Example

```typescript
import type { GenerateInvoiceVoucherRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "counterAccountId": null,
  "subjectAccountId": null,
  "taxAccountId": null,
  "word": null,
  "date": null,
} satisfies GenerateInvoiceVoucherRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as GenerateInvoiceVoucherRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


