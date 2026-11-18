
# AssistCombineDto


## Properties

Name | Type
------------ | -------------
`meta` | [LedgerMetaDto](LedgerMetaDto.md)
`assistTypeText` | string
`secondaryAssistTypeText` | string
`columns` | [Array&lt;AssistCombineCellDto&gt;](AssistCombineCellDto.md)
`groups` | [Array&lt;AssistCombineGroupDto&gt;](AssistCombineGroupDto.md)
`totalDebit` | number
`totalCredit` | number

## Example

```typescript
import type { AssistCombineDto } from ''

// TODO: Update the object below with actual values
const example = {
  "meta": null,
  "assistTypeText": null,
  "secondaryAssistTypeText": null,
  "columns": null,
  "groups": null,
  "totalDebit": null,
  "totalCredit": null,
} satisfies AssistCombineDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AssistCombineDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


