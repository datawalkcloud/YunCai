
# SalaryBillDto


## Properties

Name | Type
------------ | -------------
`id` | string
`employeeId` | string
`employeeCode` | string
`employeeName` | string
`dept` | string
`gross` | number
`deductible` | number
`payable` | number
`personalDeduction` | number
`taxThreshold` | number
`tax` | number
`adjust` | number
`net` | number
`companyPart` | number
`cost` | number
`accruedVoucherText` | string
`paidVoucherText` | string

## Example

```typescript
import type { SalaryBillDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "employeeId": null,
  "employeeCode": null,
  "employeeName": null,
  "dept": null,
  "gross": null,
  "deductible": null,
  "payable": null,
  "personalDeduction": null,
  "taxThreshold": null,
  "tax": null,
  "adjust": null,
  "net": null,
  "companyPart": null,
  "cost": null,
  "accruedVoucherText": null,
  "paidVoucherText": null,
} satisfies SalaryBillDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SalaryBillDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


