
# EmployeeInsuranceDto


## Properties

Name | Type
------------ | -------------
`insuranceItemId` | string
`insuranceBase` | number
`companyRate` | number
`personalRate` | number

## Example

```typescript
import type { EmployeeInsuranceDto } from ''

// TODO: Update the object below with actual values
const example = {
  "insuranceItemId": null,
  "insuranceBase": null,
  "companyRate": null,
  "personalRate": null,
} satisfies EmployeeInsuranceDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as EmployeeInsuranceDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


