
# VoucherDetailDto


## Properties

Name | Type
------------ | -------------
`id` | string
`word` | string
`number` | number
`voucherNo` | string
`date` | Date
`attachmentCount` | number
`remark` | string
`status` | number
`statusText` | string
`preparer` | string
`reviewer` | string
`totalDebit` | number
`totalCredit` | number
`isBalanced` | boolean
`entries` | [Array&lt;VoucherEntryDto&gt;](VoucherEntryDto.md)

## Example

```typescript
import type { VoucherDetailDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "word": null,
  "number": null,
  "voucherNo": null,
  "date": null,
  "attachmentCount": null,
  "remark": null,
  "status": null,
  "statusText": null,
  "preparer": null,
  "reviewer": null,
  "totalDebit": null,
  "totalCredit": null,
  "isBalanced": null,
  "entries": null,
} satisfies VoucherDetailDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherDetailDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


