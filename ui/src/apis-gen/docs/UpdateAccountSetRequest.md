
# UpdateAccountSetRequest


## Properties

Name | Type
------------ | -------------
`name` | string
`companyName` | string
`standard` | number
`taxpayerType` | number
`baseCurrency` | string
`accountCodingScheme` | string
`taxpayerNumber` | string
`vatRate` | number
`incomeTaxRate` | number
`voucherWords` | string
`taxRegion` | string
`taxLoginType` | string
`taxIdentityType` | string
`taxLoginAccount` | string
`taxLoginPassword` | string
`industry` | string
`assetsModuleEnabled` | boolean
`fundsModuleEnabled` | boolean
`voucherAuditRequired` | boolean
`balanceAssistScope` | number
`unitDecimalPlaces` | number
`isExportEnterprise` | boolean
`iitChannel` | string
`iitLoginType` | string
`iitLoginAccount` | string
`iitLoginPassword` | string

## Example

```typescript
import type { UpdateAccountSetRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "companyName": null,
  "standard": null,
  "taxpayerType": null,
  "baseCurrency": null,
  "accountCodingScheme": null,
  "taxpayerNumber": null,
  "vatRate": null,
  "incomeTaxRate": null,
  "voucherWords": null,
  "taxRegion": null,
  "taxLoginType": null,
  "taxIdentityType": null,
  "taxLoginAccount": null,
  "taxLoginPassword": null,
  "industry": null,
  "assetsModuleEnabled": null,
  "fundsModuleEnabled": null,
  "voucherAuditRequired": null,
  "balanceAssistScope": null,
  "unitDecimalPlaces": null,
  "isExportEnterprise": null,
  "iitChannel": null,
  "iitLoginType": null,
  "iitLoginAccount": null,
  "iitLoginPassword": null,
} satisfies UpdateAccountSetRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as UpdateAccountSetRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


