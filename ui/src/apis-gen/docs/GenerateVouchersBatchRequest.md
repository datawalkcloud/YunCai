
# GenerateVouchersBatchRequest


## Properties

Name | Type
------------ | -------------
`entryIds` | Array&lt;string&gt;
`mergeBy` | number
`counterAccountId` | string
`word` | string

## Example

```typescript
import type { GenerateVouchersBatchRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "entryIds": null,
  "mergeBy": null,
  "counterAccountId": null,
  "word": null,
} satisfies GenerateVouchersBatchRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as GenerateVouchersBatchRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


