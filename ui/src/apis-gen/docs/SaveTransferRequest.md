
# SaveTransferRequest


## Properties

Name | Type
------------ | -------------
`date` | Date
`summary` | string
`fromJournalId` | string
`toJournalId` | string
`amount` | number
`remark` | string

## Example

```typescript
import type { SaveTransferRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "summary": null,
  "fromJournalId": null,
  "toJournalId": null,
  "amount": null,
  "remark": null,
} satisfies SaveTransferRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveTransferRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


