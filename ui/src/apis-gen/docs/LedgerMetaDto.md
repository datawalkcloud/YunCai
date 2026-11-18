
# LedgerMetaDto


## Properties

Name | Type
------------ | -------------
`accountSetName` | string
`companyName` | string
`startDate` | string
`endDate` | string
`periodText` | string
`closedPeriodText` | string
`isEndPeriodClosed` | boolean
`unpostedVoucherCount` | number

## Example

```typescript
import type { LedgerMetaDto } from ''

// TODO: Update the object below with actual values
const example = {
  "accountSetName": null,
  "companyName": null,
  "startDate": null,
  "endDate": null,
  "periodText": null,
  "closedPeriodText": null,
  "isEndPeriodClosed": null,
  "unpostedVoucherCount": null,
} satisfies LedgerMetaDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as LedgerMetaDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


