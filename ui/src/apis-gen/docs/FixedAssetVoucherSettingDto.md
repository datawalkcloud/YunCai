
# FixedAssetVoucherSettingDto


## Properties

Name | Type
------------ | -------------
`depreciationExpenseAccountId` | string
`depreciationAccountId` | string
`amortizationExpenseAccountId` | string
`amortizationAccountId` | string
`disposalAccountId` | string
`depreciationExpenseAccountText` | string
`depreciationAccountText` | string
`amortizationExpenseAccountText` | string
`amortizationAccountText` | string
`disposalAccountText` | string
`depreciationResolved` | boolean
`amortizationResolved` | boolean
`disposalResolved` | boolean

## Example

```typescript
import type { FixedAssetVoucherSettingDto } from ''

// TODO: Update the object below with actual values
const example = {
  "depreciationExpenseAccountId": null,
  "depreciationAccountId": null,
  "amortizationExpenseAccountId": null,
  "amortizationAccountId": null,
  "disposalAccountId": null,
  "depreciationExpenseAccountText": null,
  "depreciationAccountText": null,
  "amortizationExpenseAccountText": null,
  "amortizationAccountText": null,
  "disposalAccountText": null,
  "depreciationResolved": null,
  "amortizationResolved": null,
  "disposalResolved": null,
} satisfies FixedAssetVoucherSettingDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FixedAssetVoucherSettingDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


