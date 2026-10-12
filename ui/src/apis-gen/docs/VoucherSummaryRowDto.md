
# VoucherSummaryRowDto


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`voucherCount` | number
`debit` | number
`credit` | number

## Example

```typescript
import type { VoucherSummaryRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "voucherCount": null,
  "debit": null,
  "credit": null,
} satisfies VoucherSummaryRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherSummaryRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


