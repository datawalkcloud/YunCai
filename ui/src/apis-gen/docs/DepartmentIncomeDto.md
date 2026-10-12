
# DepartmentIncomeDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`periodText` | string
`departments` | [Array&lt;IncomeDepartmentDto&gt;](IncomeDepartmentDto.md)
`rows` | [Array&lt;IncomeDepartmentRowDto&gt;](IncomeDepartmentRowDto.md)

## Example

```typescript
import type { DepartmentIncomeDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "periodText": null,
  "departments": null,
  "rows": null,
} satisfies DepartmentIncomeDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as DepartmentIncomeDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


