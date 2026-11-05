
# TaxInvoiceSyncResultDto


## Properties

Name | Type
------------ | -------------
`fetched` | number
`imported` | number
`skipped` | number
`needLogin` | boolean
`message` | string
`diagnostics` | Array&lt;string&gt;
`warnings` | Array&lt;string&gt;
`fetchedAt` | Date

## Example

```typescript
import type { TaxInvoiceSyncResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "fetched": null,
  "imported": null,
  "skipped": null,
  "needLogin": null,
  "message": null,
  "diagnostics": null,
  "warnings": null,
  "fetchedAt": null,
} satisfies TaxInvoiceSyncResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxInvoiceSyncResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


