
# AssistBalanceRowDto


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
`direction` | string

## Example

```typescript
import type { AssistBalanceRowDto } from ''

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
  "direction": null,
} satisfies AssistBalanceRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AssistBalanceRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


