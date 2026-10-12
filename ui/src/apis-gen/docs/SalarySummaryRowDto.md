
# SalarySummaryRowDto


## Properties

Name | Type
------------ | -------------
`dept` | string
`employeeCount` | number
`payable` | number
`personalDeduction` | number
`companyPart` | number
`net` | number
`cost` | number

## Example

```typescript
import type { SalarySummaryRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "dept": null,
  "employeeCount": null,
  "payable": null,
  "personalDeduction": null,
  "companyPart": null,
  "net": null,
  "cost": null,
} satisfies SalarySummaryRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SalarySummaryRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


