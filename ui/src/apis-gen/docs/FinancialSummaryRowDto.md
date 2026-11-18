
# FinancialSummaryRowDto


## Properties

Name | Type
------------ | -------------
`item` | string
`line` | number
`yearToDateAmount` | number
`yearToDateYoyPercent` | number
`currentAmount` | number
`currentYoyPercent` | number
`currentMomPercent` | number

## Example

```typescript
import type { FinancialSummaryRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "item": null,
  "line": null,
  "yearToDateAmount": null,
  "yearToDateYoyPercent": null,
  "currentAmount": null,
  "currentYoyPercent": null,
  "currentMomPercent": null,
} satisfies FinancialSummaryRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FinancialSummaryRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


