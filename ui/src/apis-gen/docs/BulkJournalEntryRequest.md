
# BulkJournalEntryRequest


## Properties

Name | Type
------------ | -------------
`action` | string
`entryIds` | Array&lt;string&gt;
`summary` | string
`category` | string
`counterparty` | string
`project` | string
`department` | string
`voucherId` | string

## Example

```typescript
import type { BulkJournalEntryRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "action": null,
  "entryIds": null,
  "summary": null,
  "category": null,
  "counterparty": null,
  "project": null,
  "department": null,
  "voucherId": null,
} satisfies BulkJournalEntryRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BulkJournalEntryRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


