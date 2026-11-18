
# BudgetDto


## Properties

Name | Type
------------ | -------------
`id` | string
`year` | number
`name` | string
`note` | string
`createdBy` | string
`createdAt` | Date
`updatedAt` | Date
`lines` | [Array&lt;BudgetLineDto&gt;](BudgetLineDto.md)
`total` | number

## Example

```typescript
import type { BudgetDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "year": null,
  "name": null,
  "note": null,
  "createdBy": null,
  "createdAt": null,
  "updatedAt": null,
  "lines": null,
  "total": null,
} satisfies BudgetDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BudgetDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


