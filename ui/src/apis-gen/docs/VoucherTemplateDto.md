
# VoucherTemplateDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`typeText` | string
`lines` | [Array&lt;VoucherTemplateLineDto&gt;](VoucherTemplateLineDto.md)

## Example

```typescript
import type { VoucherTemplateDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "typeText": null,
  "lines": null,
} satisfies VoucherTemplateDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherTemplateDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


