
# CarryPreviewVoucherDto


## Properties

Name | Type
------------ | -------------
`kind` | string
`word` | string
`number` | number
`label` | string
`date` | Date
`summary` | string
`totalDebit` | number
`totalCredit` | number
`lines` | [Array&lt;CarryPreviewLineDto&gt;](CarryPreviewLineDto.md)

## Example

```typescript
import type { CarryPreviewVoucherDto } from ''

// TODO: Update the object below with actual values
const example = {
  "kind": null,
  "word": null,
  "number": null,
  "label": null,
  "date": null,
  "summary": null,
  "totalDebit": null,
  "totalCredit": null,
  "lines": null,
} satisfies CarryPreviewVoucherDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CarryPreviewVoucherDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


