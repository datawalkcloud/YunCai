
# TaxIitReportResultDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`rows` | [Array&lt;TaxIitReportRowDto&gt;](TaxIitReportRowDto.md)
`personCount` | number
`totalIncome` | number
`totalRefundable` | number
`message` | string

## Example

```typescript
import type { TaxIitReportResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "rows": null,
  "personCount": null,
  "totalIncome": null,
  "totalRefundable": null,
  "message": null,
} satisfies TaxIitReportResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxIitReportResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


