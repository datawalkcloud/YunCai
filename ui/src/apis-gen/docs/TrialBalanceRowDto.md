
# TrialBalanceRowDto


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`openingDebit` | number
`openingCredit` | number
`periodDebit` | number
`periodCredit` | number
`endingDebit` | number
`endingCredit` | number

## Example

```typescript
import type { TrialBalanceRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "openingDebit": null,
  "openingCredit": null,
  "periodDebit": null,
  "periodCredit": null,
  "endingDebit": null,
  "endingCredit": null,
} satisfies TrialBalanceRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TrialBalanceRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


