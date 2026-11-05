
# VoucherQuery


## Properties

Name | Type
------------ | -------------
`startDate` | Date
`endDate` | Date
`word` | string
`preparer` | string
`hasAssists` | boolean
`summaryContains` | string
`remarkContains` | string
`accountId` | string
`direction` | boolean
`amountFrom` | number
`amountTo` | number
`numberFrom` | number
`numberTo` | number
`sortBy` | string
`sortDesc` | boolean
`deleted` | boolean

## Example

```typescript
import type { VoucherQuery } from ''

// TODO: Update the object below with actual values
const example = {
  "startDate": null,
  "endDate": null,
  "word": null,
  "preparer": null,
  "hasAssists": null,
  "summaryContains": null,
  "remarkContains": null,
  "accountId": null,
  "direction": null,
  "amountFrom": null,
  "amountTo": null,
  "numberFrom": null,
  "numberTo": null,
  "sortBy": null,
  "sortDesc": null,
  "deleted": null,
} satisfies VoucherQuery

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherQuery
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


