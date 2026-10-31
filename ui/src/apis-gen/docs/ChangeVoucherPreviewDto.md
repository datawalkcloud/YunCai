
# ChangeVoucherPreviewDto


## Properties

Name | Type
------------ | -------------
`changeId` | string
`kindText` | string
`assetCode` | string
`assetName` | string
`date` | Date
`entries` | [Array&lt;ChangeVoucherEntryDto&gt;](ChangeVoucherEntryDto.md)
`hint` | string

## Example

```typescript
import type { ChangeVoucherPreviewDto } from ''

// TODO: Update the object below with actual values
const example = {
  "changeId": null,
  "kindText": null,
  "assetCode": null,
  "assetName": null,
  "date": null,
  "entries": null,
  "hint": null,
} satisfies ChangeVoucherPreviewDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ChangeVoucherPreviewDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


