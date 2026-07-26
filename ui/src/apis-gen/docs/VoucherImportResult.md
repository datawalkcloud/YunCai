
# VoucherImportResult


## Properties

Name | Type
------------ | -------------
`totalRows` | number
`importedVouchers` | number
`importedEntries` | number
`skippedVouchers` | number
`overwrittenVouchers` | number
`createdAccounts` | number
`warnings` | Array&lt;string&gt;

## Example

```typescript
import type { VoucherImportResult } from ''

// TODO: Update the object below with actual values
const example = {
  "totalRows": null,
  "importedVouchers": null,
  "importedEntries": null,
  "skippedVouchers": null,
  "overwrittenVouchers": null,
  "createdAccounts": null,
  "warnings": null,
} satisfies VoucherImportResult

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherImportResult
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


