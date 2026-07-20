
# TrialBalanceDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`periodText` | string
`rows` | [Array&lt;TrialBalanceRowDto&gt;](TrialBalanceRowDto.md)

## Example

```typescript
import type { TrialBalanceDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "periodText": null,
  "rows": null,
} satisfies TrialBalanceDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TrialBalanceDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


