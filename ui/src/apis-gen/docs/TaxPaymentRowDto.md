
# TaxPaymentRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`project` | string
`item` | string
`periodStart` | string
`periodEnd` | string
`amount` | number
`taxKind` | string
`paidOn` | string
`method` | string
`voucherKind` | string
`voucherNo` | string
`eTicketNo` | string

## Example

```typescript
import type { TaxPaymentRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "project": null,
  "item": null,
  "periodStart": null,
  "periodEnd": null,
  "amount": null,
  "taxKind": null,
  "paidOn": null,
  "method": null,
  "voucherKind": null,
  "voucherNo": null,
  "eTicketNo": null,
} satisfies TaxPaymentRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxPaymentRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


