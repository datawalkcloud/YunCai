
# ChangeVoucherEntryDto


## Properties

Name | Type
------------ | -------------
`summary` | string
`accountId` | string
`accountText` | string
`debit` | number
`credit` | number

## Example

```typescript
import type { ChangeVoucherEntryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "summary": null,
  "accountId": null,
  "accountText": null,
  "debit": null,
  "credit": null,
} satisfies ChangeVoucherEntryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ChangeVoucherEntryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


