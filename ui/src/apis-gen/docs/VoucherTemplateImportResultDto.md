
# VoucherTemplateImportResultDto


## Properties

Name | Type
------------ | -------------
`imported` | number
`skipped` | number
`failures` | [Array&lt;VoucherTemplateImportFailureDto&gt;](VoucherTemplateImportFailureDto.md)

## Example

```typescript
import type { VoucherTemplateImportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "imported": null,
  "skipped": null,
  "failures": null,
} satisfies VoucherTemplateImportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherTemplateImportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


