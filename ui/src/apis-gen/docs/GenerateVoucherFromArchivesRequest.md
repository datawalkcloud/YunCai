
# GenerateVoucherFromArchivesRequest


## Properties

Name | Type
------------ | -------------
`ids` | Array&lt;string&gt;
`date` | Date
`word` | string
`summary` | string
`amount` | number
`debitAccountId` | string
`creditAccountId` | string

## Example

```typescript
import type { GenerateVoucherFromArchivesRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "ids": null,
  "date": null,
  "word": null,
  "summary": null,
  "amount": null,
  "debitAccountId": null,
  "creditAccountId": null,
} satisfies GenerateVoucherFromArchivesRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as GenerateVoucherFromArchivesRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


