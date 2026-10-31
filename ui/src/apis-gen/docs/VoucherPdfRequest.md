
# VoucherPdfRequest


## Properties

Name | Type
------------ | -------------
`ids` | Array&lt;string&gt;
`query` | [VoucherQuery](VoucherQuery.md)
`paper` | string
`orientation` | string
`valign` | string
`rows` | number
`fontSize` | number
`marginLeft` | number
`marginRight` | number
`marginTop` | number
`marginBottom` | number
`signPreparer` | boolean
`preparerText` | string
`signReviewer` | boolean
`reviewerText` | string
`signManager` | boolean
`managerText` | string
`signBookkeeper` | boolean
`bookkeeperText` | string
`signCashier` | boolean
`cashierText` | string
`signTime` | boolean
`showAccountCode` | boolean
`zeroAttachment` | boolean
`mergePrint` | boolean

## Example

```typescript
import type { VoucherPdfRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "ids": null,
  "query": null,
  "paper": null,
  "orientation": null,
  "valign": null,
  "rows": null,
  "fontSize": null,
  "marginLeft": null,
  "marginRight": null,
  "marginTop": null,
  "marginBottom": null,
  "signPreparer": null,
  "preparerText": null,
  "signReviewer": null,
  "reviewerText": null,
  "signManager": null,
  "managerText": null,
  "signBookkeeper": null,
  "bookkeeperText": null,
  "signCashier": null,
  "cashierText": null,
  "signTime": null,
  "showAccountCode": null,
  "zeroAttachment": null,
  "mergePrint": null,
} satisfies VoucherPdfRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherPdfRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


