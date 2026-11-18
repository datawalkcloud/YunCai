
# CreateVoucherRequest


## Properties

Name | Type
------------ | -------------
`word` | string
`number` | number
`date` | Date
`attachmentCount` | number
`remark` | string
`entries` | [Array&lt;CreateVoucherEntryRequest&gt;](CreateVoucherEntryRequest.md)

## Example

```typescript
import type { CreateVoucherRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "word": null,
  "number": null,
  "date": null,
  "attachmentCount": null,
  "remark": null,
  "entries": null,
} satisfies CreateVoucherRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CreateVoucherRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


