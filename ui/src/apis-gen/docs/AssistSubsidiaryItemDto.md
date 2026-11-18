
# AssistSubsidiaryItemDto


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`totalDebit` | number
`totalCredit` | number
`rows` | [Array&lt;SubsidiaryLedgerRowDto&gt;](SubsidiaryLedgerRowDto.md)

## Example

```typescript
import type { AssistSubsidiaryItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "totalDebit": null,
  "totalCredit": null,
  "rows": null,
} satisfies AssistSubsidiaryItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AssistSubsidiaryItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


