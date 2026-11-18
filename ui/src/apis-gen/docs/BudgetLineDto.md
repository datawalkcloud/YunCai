
# BudgetLineDto


## Properties

Name | Type
------------ | -------------
`accountId` | string
`accountCode` | string
`accountName` | string
`isLeaf` | boolean
`months` | Array&lt;number&gt;
`total` | number

## Example

```typescript
import type { BudgetLineDto } from ''

// TODO: Update the object below with actual values
const example = {
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "isLeaf": null,
  "months": null,
  "total": null,
} satisfies BudgetLineDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BudgetLineDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


