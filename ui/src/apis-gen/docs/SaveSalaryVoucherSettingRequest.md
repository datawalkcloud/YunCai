
# SaveSalaryVoucherSettingRequest


## Properties

Name | Type
------------ | -------------
`payrollAccountId` | string
`taxAccountId` | string
`insuranceAccountId` | string
`bankAccountId` | string

## Example

```typescript
import type { SaveSalaryVoucherSettingRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "payrollAccountId": null,
  "taxAccountId": null,
  "insuranceAccountId": null,
  "bankAccountId": null,
} satisfies SaveSalaryVoucherSettingRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveSalaryVoucherSettingRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


