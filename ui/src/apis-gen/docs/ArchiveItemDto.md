
# ArchiveItemDto


## Properties

Name | Type
------------ | -------------
`id` | string
`fileName` | string
`category` | string
`docType` | string
`uploadedAt` | Date
`fileSize` | number
`relatedDoc` | string
`relatedNo` | string
`voucherText` | string
`matter` | string
`checkStatus` | string
`statusText` | string
`uploader` | string
`source` | string
`isArchived` | boolean
`isPaper` | boolean
`archivedAt` | Date
`archivedBy` | string

## Example

```typescript
import type { ArchiveItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "fileName": null,
  "category": null,
  "docType": null,
  "uploadedAt": null,
  "fileSize": null,
  "relatedDoc": null,
  "relatedNo": null,
  "voucherText": null,
  "matter": null,
  "checkStatus": null,
  "statusText": null,
  "uploader": null,
  "source": null,
  "isArchived": null,
  "isPaper": null,
  "archivedAt": null,
  "archivedBy": null,
} satisfies ArchiveItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ArchiveItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


