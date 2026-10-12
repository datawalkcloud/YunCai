
# CustomReportRenderDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`name` | string
`valueType` | number
`year` | number
`rows` | [Array&lt;CustomReportRenderRowDto&gt;](CustomReportRenderRowDto.md)

## Example

```typescript
import type { CustomReportRenderDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "name": null,
  "valueType": null,
  "year": null,
  "rows": null,
} satisfies CustomReportRenderDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CustomReportRenderDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


