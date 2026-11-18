
# FundReportRowDto


## Properties

Name | Type
------------ | -------------
`name` | string
`code` | string
`openingBalance` | number
`totalIncome` | number
`totalExpense` | number
`balance` | number
`incomeCount` | number
`expenseCount` | number
`secondaryCode` | string
`secondaryName` | string

## Example

```typescript
import type { FundReportRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "code": null,
  "openingBalance": null,
  "totalIncome": null,
  "totalExpense": null,
  "balance": null,
  "incomeCount": null,
  "expenseCount": null,
  "secondaryCode": null,
  "secondaryName": null,
} satisfies FundReportRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FundReportRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


