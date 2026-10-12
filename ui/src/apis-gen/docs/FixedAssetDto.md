
# FixedAssetDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`categoryId` | string
`categoryName` | string
`attrText` | string
`name` | string
`model` | string
`dept` | string
`startDate` | Date
`originValue` | number
`accumulatedDepreciation` | number
`netValue` | number
`statusText` | string
`clearedDate` | Date

## Example

```typescript
import type { FixedAssetDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "categoryId": null,
  "categoryName": null,
  "attrText": null,
  "name": null,
  "model": null,
  "dept": null,
  "startDate": null,
  "originValue": null,
  "accumulatedDepreciation": null,
  "netValue": null,
  "statusText": null,
  "clearedDate": null,
} satisfies FixedAssetDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FixedAssetDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


