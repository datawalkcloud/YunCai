
# JournalDetailDto


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
`totalIncome` | number
`totalExpense` | number
`closingBalance` | number
`entries` | [Array&lt;JournalEntryDto&gt;](JournalEntryDto.md)

## Example

```typescript
import type { JournalDetailDto } from ''

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
  "totalIncome": null,
  "totalExpense": null,
  "closingBalance": null,
  "entries": null,
} satisfies JournalDetailDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as JournalDetailDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


