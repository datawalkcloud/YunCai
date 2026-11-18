
# ClosingTemplatePreviewItemDto


## Properties

Name | Type
------------ | -------------
`templateId` | string
`name` | string
`isEnabled` | boolean
`lines` | [Array&lt;ClosingTemplatePreviewLineDto&gt;](ClosingTemplatePreviewLineDto.md)
`totalDebit` | number
`totalCredit` | number
`balanced` | boolean
`error` | string

## Example

```typescript
import type { ClosingTemplatePreviewItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "templateId": null,
  "name": null,
  "isEnabled": null,
  "lines": null,
  "totalDebit": null,
  "totalCredit": null,
  "balanced": null,
  "error": null,
} satisfies ClosingTemplatePreviewItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ClosingTemplatePreviewItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


