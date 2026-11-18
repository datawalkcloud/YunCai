
# BudgetExecutionDto


## Properties

Name | Type
------------ | -------------
`budgetId` | string
`year` | number
`name` | string
`note` | string
`throughMonth` | number
`rows` | [Array&lt;BudgetExecutionRowDto&gt;](BudgetExecutionRowDto.md)
`budgetMonths` | Array&lt;number&gt;
`actualMonths` | Array&lt;number&gt;
`budgetToDate` | number
`actualToDate` | number
`variance` | number
`rate` | number

## Example

```typescript
import type { BudgetExecutionDto } from ''

// TODO: Update the object below with actual values
const example = {
  "budgetId": null,
  "year": null,
  "name": null,
  "note": null,
  "throughMonth": null,
  "rows": null,
  "budgetMonths": null,
  "actualMonths": null,
  "budgetToDate": null,
  "actualToDate": null,
  "variance": null,
  "rate": null,
} satisfies BudgetExecutionDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BudgetExecutionDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


