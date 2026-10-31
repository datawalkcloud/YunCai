
# CreateEntryCategoryRequest


## Properties

Name | Type
------------ | -------------
`type` | number
`code` | string
`name` | string
`parentId` | string
`cashFlowItemId` | string
`smartKeywords` | string
`voucherSubjectId` | string

## Example

```typescript
import type { CreateEntryCategoryRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "type": null,
  "code": null,
  "name": null,
  "parentId": null,
  "cashFlowItemId": null,
  "smartKeywords": null,
  "voucherSubjectId": null,
} satisfies CreateEntryCategoryRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CreateEntryCategoryRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


