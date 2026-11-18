
# SaveFixedAssetRequest


## Properties

Name | Type
------------ | -------------
`code` | string
`categoryId` | string
`name` | string
`model` | string
`dept` | string
`startDate` | Date
`originValue` | number
`initialAccumulatedDepreciation` | number

## Example

```typescript
import type { SaveFixedAssetRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "categoryId": null,
  "name": null,
  "model": null,
  "dept": null,
  "startDate": null,
  "originValue": null,
  "initialAccumulatedDepreciation": null,
} satisfies SaveFixedAssetRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveFixedAssetRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


