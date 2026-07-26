
# OpeningBalancesDto


## Properties

Name | Type
------------ | -------------
`rows` | [Array&lt;OpeningBalanceRowDto&gt;](OpeningBalanceRowDto.md)
`totalDebit` | number
`totalCredit` | number
`balanced` | boolean

## Example

```typescript
import type { OpeningBalancesDto } from ''

// TODO: Update the object below with actual values
const example = {
  "rows": null,
  "totalDebit": null,
  "totalCredit": null,
  "balanced": null,
} satisfies OpeningBalancesDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as OpeningBalancesDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


