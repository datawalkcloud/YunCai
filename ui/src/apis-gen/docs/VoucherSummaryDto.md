
# VoucherSummaryDto


## Properties

Name | Type
------------ | -------------
`meta` | [LedgerMetaDto](LedgerMetaDto.md)
`voucherCount` | number
`attachmentCount` | number
`rows` | [Array&lt;VoucherSummaryRowDto&gt;](VoucherSummaryRowDto.md)
`totalDebit` | number
`totalCredit` | number

## Example

```typescript
import type { VoucherSummaryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "meta": null,
  "voucherCount": null,
  "attachmentCount": null,
  "rows": null,
  "totalDebit": null,
  "totalCredit": null,
} satisfies VoucherSummaryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherSummaryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


