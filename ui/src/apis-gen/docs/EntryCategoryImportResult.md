
# EntryCategoryImportResult


## Properties

Name | Type
------------ | -------------
`totalRows` | number
`imported` | number
`skipped` | number
`warnings` | Array&lt;string&gt;

## Example

```typescript
import type { EntryCategoryImportResult } from ''

// TODO: Update the object below with actual values
const example = {
  "totalRows": null,
  "imported": null,
  "skipped": null,
  "warnings": null,
} satisfies EntryCategoryImportResult

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as EntryCategoryImportResult
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


