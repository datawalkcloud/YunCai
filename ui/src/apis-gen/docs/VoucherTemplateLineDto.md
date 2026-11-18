
# VoucherTemplateLineDto


## Properties

Name | Type
------------ | -------------
`id` | string
`lineNumber` | number
`summary` | string
`accountId` | string
`accountCode` | string
`accountName` | string
`directionText` | string
`isDebit` | boolean

## Example

```typescript
import type { VoucherTemplateLineDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "lineNumber": null,
  "summary": null,
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "directionText": null,
  "isDebit": null,
} satisfies VoucherTemplateLineDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherTemplateLineDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


