
# TaxIitPersonnelResultDto


## Properties

Name | Type
------------ | -------------
`rows` | [Array&lt;TaxIitPersonnelRowDto&gt;](TaxIitPersonnelRowDto.md)
`message` | string
`needLogin` | boolean
`diagnostics` | Array&lt;string&gt;
`fetchedAt` | Date

## Example

```typescript
import type { TaxIitPersonnelResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "rows": null,
  "message": null,
  "needLogin": null,
  "diagnostics": null,
  "fetchedAt": null,
} satisfies TaxIitPersonnelResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxIitPersonnelResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


