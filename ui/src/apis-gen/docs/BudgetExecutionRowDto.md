
# BudgetExecutionRowDto


## Properties

Name | Type
------------ | -------------
`accountId` | string
`accountCode` | string
`accountName` | string
`direction` | number
`budgetMonths` | Array&lt;number&gt;
`actualMonths` | Array&lt;number&gt;
`budgetToDate` | number
`actualToDate` | number
`variance` | number
`rate` | number

## Example

```typescript
import type { BudgetExecutionRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "direction": null,
  "budgetMonths": null,
  "actualMonths": null,
  "budgetToDate": null,
  "actualToDate": null,
  "variance": null,
  "rate": null,
} satisfies BudgetExecutionRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BudgetExecutionRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


