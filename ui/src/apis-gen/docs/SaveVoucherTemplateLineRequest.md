
# SaveVoucherTemplateLineRequest


## Properties

Name | Type
------------ | -------------
`summary` | string
`accountId` | string
`isDebit` | boolean

## Example

```typescript
import type { SaveVoucherTemplateLineRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "summary": null,
  "accountId": null,
  "isDebit": null,
} satisfies SaveVoucherTemplateLineRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveVoucherTemplateLineRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


