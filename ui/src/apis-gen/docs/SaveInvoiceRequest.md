
# SaveInvoiceRequest


## Properties

Name | Type
------------ | -------------
`invoiceType` | string
`inputDate` | Date
`issueDate` | Date
`invoiceNo` | string
`partnerName` | string
`bizType` | string
`remark` | string
`lines` | [Array&lt;SaveInvoiceLineRequest&gt;](SaveInvoiceLineRequest.md)

## Example

```typescript
import type { SaveInvoiceRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "invoiceType": null,
  "inputDate": null,
  "issueDate": null,
  "invoiceNo": null,
  "partnerName": null,
  "bizType": null,
  "remark": null,
  "lines": null,
} satisfies SaveInvoiceRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveInvoiceRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


