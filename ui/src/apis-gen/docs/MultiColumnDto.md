
# MultiColumnDto


## Properties

Name | Type
------------ | -------------
`meta` | [LedgerMetaDto](LedgerMetaDto.md)
`code` | string
`name` | string
`path` | string
`columns` | [Array&lt;MultiColumnHeaderDto&gt;](MultiColumnHeaderDto.md)
`rows` | [Array&lt;MultiColumnRowDto&gt;](MultiColumnRowDto.md)
`totalDebit` | number
`totalCredit` | number

## Example

```typescript
import type { MultiColumnDto } from ''

// TODO: Update the object below with actual values
const example = {
  "meta": null,
  "code": null,
  "name": null,
  "path": null,
  "columns": null,
  "rows": null,
  "totalDebit": null,
  "totalCredit": null,
} satisfies MultiColumnDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MultiColumnDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


