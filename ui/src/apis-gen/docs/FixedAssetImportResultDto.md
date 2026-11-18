
# FixedAssetImportResultDto


## Properties

Name | Type
------------ | -------------
`imported` | number
`updated` | number
`failures` | [Array&lt;FixedAssetImportFailureDto&gt;](FixedAssetImportFailureDto.md)

## Example

```typescript
import type { FixedAssetImportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "imported": null,
  "updated": null,
  "failures": null,
} satisfies FixedAssetImportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FixedAssetImportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


