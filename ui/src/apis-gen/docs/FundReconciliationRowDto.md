
# FundReconciliationRowDto


## Properties

Name | Type
------------ | -------------
`journalId` | string
`journalName` | string
`journalType` | number
`projectText` | string
`accountCode` | string
`accountName` | string
`currency` | string
`openingBalance` | number
`debit` | number
`credit` | number
`balance` | number
`ledgerBalance` | number
`difference` | number
`balanced` | boolean
`note` | string

## Example

```typescript
import type { FundReconciliationRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "journalId": null,
  "journalName": null,
  "journalType": null,
  "projectText": null,
  "accountCode": null,
  "accountName": null,
  "currency": null,
  "openingBalance": null,
  "debit": null,
  "credit": null,
  "balance": null,
  "ledgerBalance": null,
  "difference": null,
  "balanced": null,
  "note": null,
} satisfies FundReconciliationRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FundReconciliationRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


