
# FundReconciliationDto


## Properties

Name | Type
------------ | -------------
`startDate` | Date
`date` | Date
`allBalanced` | boolean
`hasUnlinkedAccounts` | boolean
`rows` | [Array&lt;FundReconciliationRowDto&gt;](FundReconciliationRowDto.md)

## Example

```typescript
import type { FundReconciliationDto } from ''

// TODO: Update the object below with actual values
const example = {
  "startDate": null,
  "date": null,
  "allBalanced": null,
  "hasUnlinkedAccounts": null,
  "rows": null,
} satisfies FundReconciliationDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FundReconciliationDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


