
# SaveFixedAssetVoucherSettingRequest


## Properties

Name | Type
------------ | -------------
`depreciationExpenseAccountId` | string
`depreciationAccountId` | string
`amortizationExpenseAccountId` | string
`amortizationAccountId` | string
`disposalAccountId` | string

## Example

```typescript
import type { SaveFixedAssetVoucherSettingRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "depreciationExpenseAccountId": null,
  "depreciationAccountId": null,
  "amortizationExpenseAccountId": null,
  "amortizationAccountId": null,
  "disposalAccountId": null,
} satisfies SaveFixedAssetVoucherSettingRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveFixedAssetVoucherSettingRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


