
# VoucherAttachmentDto


## Properties

Name | Type
------------ | -------------
`id` | string
`fileName` | string
`contentType` | string
`sizeBytes` | number
`uploadedAt` | Date

## Example

```typescript
import type { VoucherAttachmentDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "fileName": null,
  "contentType": null,
  "sizeBytes": null,
  "uploadedAt": null,
} satisfies VoucherAttachmentDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as VoucherAttachmentDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


