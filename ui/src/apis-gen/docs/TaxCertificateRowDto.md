
# TaxCertificateRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`name` | string
`voucherNo` | string
`authority` | string
`periodStart` | string
`periodEnd` | string
`paidOn` | string
`amount` | number
`issuedOn` | string
`format` | string

## Example

```typescript
import type { TaxCertificateRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "name": null,
  "voucherNo": null,
  "authority": null,
  "periodStart": null,
  "periodEnd": null,
  "paidOn": null,
  "amount": null,
  "issuedOn": null,
  "format": null,
} satisfies TaxCertificateRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxCertificateRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


