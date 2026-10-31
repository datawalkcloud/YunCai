
# ExpenseImportResultDto


## Properties

Name | Type
------------ | -------------
`imported` | number
`updated` | number
`failures` | [Array&lt;ExpenseImportFailureDto&gt;](ExpenseImportFailureDto.md)

## Example

```typescript
import type { ExpenseImportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "imported": null,
  "updated": null,
  "failures": null,
} satisfies ExpenseImportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ExpenseImportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


