
# TaxIitReportRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`code` | string
`name` | string
`idNo` | string
`currentIncome` | number
`standardDeduction` | number
`cumulativeIncome` | number
`cumulativeStandardDeduction` | number
`cumulativeSpecialDeduction` | number
`remark` | string
`taxableIncome` | number
`rate` | number
`quickDeduction` | number
`cumulativeTax` | number
`paidTax` | number
`refundable` | number

## Example

```typescript
import type { TaxIitReportRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "code": null,
  "name": null,
  "idNo": null,
  "currentIncome": null,
  "standardDeduction": null,
  "cumulativeIncome": null,
  "cumulativeStandardDeduction": null,
  "cumulativeSpecialDeduction": null,
  "remark": null,
  "taxableIncome": null,
  "rate": null,
  "quickDeduction": null,
  "cumulativeTax": null,
  "paidTax": null,
  "refundable": null,
} satisfies TaxIitReportRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxIitReportRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


