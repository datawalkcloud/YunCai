
# SaveClosingTemplateLineRequest


## Properties

Name | Type
------------ | -------------
`summary` | string
`accountId` | string
`direction` | number
`sourceType` | number
`sourceAccountId` | string
`amount` | number

## Example

```typescript
import type { SaveClosingTemplateLineRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "summary": null,
  "accountId": null,
  "direction": null,
  "sourceType": null,
  "sourceAccountId": null,
  "amount": null,
} satisfies SaveClosingTemplateLineRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveClosingTemplateLineRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


