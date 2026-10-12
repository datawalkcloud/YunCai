
# InvoiceDto


## Properties

Name | Type
------------ | -------------
`id` | string
`invoiceType` | string
`inputDate` | Date
`issueDate` | Date
`invoiceNo` | string
`partnerName` | string
`bizType` | string
`authDate` | Date
`taxPeriod` | number
`totalAmount` | number
`totalTax` | number
`totalWithTax` | number
`voucherText` | string
`source` | string
`statusText` | string
`lines` | [Array&lt;InvoiceLineDto&gt;](InvoiceLineDto.md)

## Example

```typescript
import type { InvoiceDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "invoiceType": null,
  "inputDate": null,
  "issueDate": null,
  "invoiceNo": null,
  "partnerName": null,
  "bizType": null,
  "authDate": null,
  "taxPeriod": null,
  "totalAmount": null,
  "totalTax": null,
  "totalWithTax": null,
  "voucherText": null,
  "source": null,
  "statusText": null,
  "lines": null,
} satisfies InvoiceDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as InvoiceDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


