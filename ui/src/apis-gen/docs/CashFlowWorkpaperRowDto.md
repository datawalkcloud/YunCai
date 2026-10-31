
# CashFlowWorkpaperRowDto


## Properties

Name | Type
------------ | -------------
`date` | Date
`voucherText` | string
`summary` | string
`accountCode` | string
`accountName` | string
`line` | number
`lineName` | string
`inflow` | boolean
`amount` | number

## Example

```typescript
import type { CashFlowWorkpaperRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "voucherText": null,
  "summary": null,
  "accountCode": null,
  "accountName": null,
  "line": null,
  "lineName": null,
  "inflow": null,
  "amount": null,
} satisfies CashFlowWorkpaperRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashFlowWorkpaperRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


