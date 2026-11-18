
# JournalAccountImportResultDto


## Properties

Name | Type
------------ | -------------
`imported` | number
`updated` | number
`failures` | [Array&lt;JournalAccountImportFailureDto&gt;](JournalAccountImportFailureDto.md)

## Example

```typescript
import type { JournalAccountImportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "imported": null,
  "updated": null,
  "failures": null,
} satisfies JournalAccountImportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as JournalAccountImportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


