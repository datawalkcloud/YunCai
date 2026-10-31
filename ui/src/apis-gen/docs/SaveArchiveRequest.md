
# SaveArchiveRequest


## Properties

Name | Type
------------ | -------------
`fileName` | string
`category` | string
`docType` | string
`fileBase64` | string
`relatedDoc` | string
`relatedNo` | string
`voucherText` | string
`matter` | string
`checkStatus` | string
`uploader` | string
`source` | string
`isPaper` | boolean

## Example

```typescript
import type { SaveArchiveRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "fileName": null,
  "category": null,
  "docType": null,
  "fileBase64": null,
  "relatedDoc": null,
  "relatedNo": null,
  "voucherText": null,
  "matter": null,
  "checkStatus": null,
  "uploader": null,
  "source": null,
  "isPaper": null,
} satisfies SaveArchiveRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveArchiveRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


