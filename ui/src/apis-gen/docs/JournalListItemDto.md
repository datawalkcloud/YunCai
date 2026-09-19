
# JournalListItemDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`type` | number
`typeText` | string
`accountId` | string
`accountCode` | string
`accountName` | string
`openingBalance` | number
`closingBalance` | number
`totalIncome` | number
`totalExpense` | number
`entryCount` | number

## Example

```typescript
import type { JournalListItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "type": null,
  "typeText": null,
  "accountId": null,
  "accountCode": null,
  "accountName": null,
  "openingBalance": null,
  "closingBalance": null,
  "totalIncome": null,
  "totalExpense": null,
  "entryCount": null,
} satisfies JournalListItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as JournalListItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


