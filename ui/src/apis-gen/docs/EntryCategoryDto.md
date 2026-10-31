
# EntryCategoryDto


## Properties

Name | Type
------------ | -------------
`id` | string
`type` | number
`typeText` | string
`code` | string
`name` | string
`parentId` | string
`parentName` | string
`cashFlowItemId` | string
`cashFlowName` | string
`isEnabled` | boolean
`smartKeywords` | string
`voucherSubjectId` | string
`voucherSubjectCode` | string
`voucherSubjectName` | string

## Example

```typescript
import type { EntryCategoryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "type": null,
  "typeText": null,
  "code": null,
  "name": null,
  "parentId": null,
  "parentName": null,
  "cashFlowItemId": null,
  "cashFlowName": null,
  "isEnabled": null,
  "smartKeywords": null,
  "voucherSubjectId": null,
  "voucherSubjectCode": null,
  "voucherSubjectName": null,
} satisfies EntryCategoryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as EntryCategoryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


