
# SaveFixedAssetCategoryRequest


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`method` | number
`attr` | number
`subjectAccountId` | string
`months` | number
`residualRate` | number

## Example

```typescript
import type { SaveFixedAssetCategoryRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "method": null,
  "attr": null,
  "subjectAccountId": null,
  "months": null,
  "residualRate": null,
} satisfies SaveFixedAssetCategoryRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveFixedAssetCategoryRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


