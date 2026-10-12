
# ClosingTemplateLineDto


## Properties

Name | Type
------------ | -------------
`id` | string
`lineNumber` | number
`summary` | string
`accountId` | string
`accountCode` | string
`accountName` | string
`direction` | number
`directionText` | string
`sourceType` | number
`sourceTypeText` | string
`sourceAccountId` | string
`sourceAccountCode` | string
`sourceAccountName` | string
`amount` | number

## Example

```typescript
import type { ClosingTemplateLineDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "lineNumber": null,
  "summary": null,
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "direction": null,
  "directionText": null,
  "sourceType": null,
  "sourceTypeText": null,
  "sourceAccountId": null,
  "sourceAccountCode": null,
  "sourceAccountName": null,
  "amount": null,
} satisfies ClosingTemplateLineDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ClosingTemplateLineDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


