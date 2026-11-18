
# OpeningBalanceImportResultDto


## Properties

Name | Type
------------ | -------------
`imported` | number
`updated` | number
`totalDebit` | number
`totalCredit` | number
`balanced` | boolean
`failures` | [Array&lt;OpeningBalanceImportFailureDto&gt;](OpeningBalanceImportFailureDto.md)

## Example

```typescript
import type { OpeningBalanceImportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "imported": null,
  "updated": null,
  "totalDebit": null,
  "totalCredit": null,
  "balanced": null,
  "failures": null,
} satisfies OpeningBalanceImportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as OpeningBalanceImportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


