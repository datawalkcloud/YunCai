
# SaveSalaryBillRequest


## Properties

Name | Type
------------ | -------------
`period` | number
`employeeId` | string
`gross` | number
`deductible` | number
`personalDeduction` | number
`taxThreshold` | number
`tax` | number
`adjust` | number
`companyPart` | number

## Example

```typescript
import type { SaveSalaryBillRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "period": null,
  "employeeId": null,
  "gross": null,
  "deductible": null,
  "personalDeduction": null,
  "taxThreshold": null,
  "tax": null,
  "adjust": null,
  "companyPart": null,
} satisfies SaveSalaryBillRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveSalaryBillRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


