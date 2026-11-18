
# CashJournalDto


## Properties

Name | Type
------------ | -------------
`startDate` | string
`endDate` | string
`periodText` | string
`openingBalance` | number
`rows` | [Array&lt;CashJournalRowDto&gt;](CashJournalRowDto.md)

## Example

```typescript
import type { CashJournalDto } from ''

// TODO: Update the object below with actual values
const example = {
  "startDate": null,
  "endDate": null,
  "periodText": null,
  "openingBalance": null,
  "rows": null,
} satisfies CashJournalDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashJournalDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


