
# ClosingTemplateDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`isEnabled` | boolean
`lines` | [Array&lt;ClosingTemplateLineDto&gt;](ClosingTemplateLineDto.md)

## Example

```typescript
import type { ClosingTemplateDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "isEnabled": null,
  "lines": null,
} satisfies ClosingTemplateDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ClosingTemplateDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


