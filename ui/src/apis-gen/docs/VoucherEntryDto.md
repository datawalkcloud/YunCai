
# VoucherEntryDto


## Properties

Name | Type
------------ | -------------
`id` | string
`lineNumber` | number
`summary` | string
`accountId` | string
`accountCode` | string
`accountName` | string
`debit` | number
`credit` | number

## Example

```typescript
import type { VoucherEntryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "lineNumber": null,
  "summary": null,
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "debit": null,
  "credit": null,
} satisfies VoucherEntryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherEntryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


