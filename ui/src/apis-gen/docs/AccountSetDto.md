
# AccountSetDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`companyName` | string
`standard` | number
`taxpayerType` | number
`baseCurrency` | string
`startYear` | number
`startMonth` | number
`startPeriod` | string
`isCurrent` | boolean
`accountCodingScheme` | string
`taxpayerNumber` | string
`vatRate` | number
`incomeTaxRate` | number
`cityBuildTaxRate` | number
`educationSurchargeRate` | number
`localEducationSurchargeRate` | number
`taxRegion` | string
`taxLoginType` | string
`taxIdentityType` | string
`taxLoginAccount` | string
`taxLoginPasswordSet` | boolean
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
`iitLoginPasswordSet` | boolean
`voucherWords` | string
`closedPeriod` | string
`carriedPeriod` | string
`nextCloseYear` | number
`nextCloseMonth` | number

## Example

```typescript
import type { AccountSetDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "companyName": null,
  "standard": null,
  "taxpayerType": null,
  "baseCurrency": null,
  "startYear": null,
  "startMonth": null,
  "startPeriod": null,
  "isCurrent": null,
  "accountCodingScheme": null,
  "taxpayerNumber": null,
  "vatRate": null,
  "incomeTaxRate": null,
  "cityBuildTaxRate": null,
  "educationSurchargeRate": null,
  "localEducationSurchargeRate": null,
  "taxRegion": null,
  "taxLoginType": null,
  "taxIdentityType": null,
  "taxLoginAccount": null,
  "taxLoginPasswordSet": null,
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
  "iitLoginPasswordSet": null,
  "voucherWords": null,
  "closedPeriod": null,
  "carriedPeriod": null,
  "nextCloseYear": null,
  "nextCloseMonth": null,
} satisfies AccountSetDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AccountSetDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


