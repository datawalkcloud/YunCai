
# UpdateVoucherRequest


## Properties

Name | Type
------------ | -------------
`date` | Date
`attachmentCount` | number
`entries` | [Array&lt;CreateVoucherEntryRequest&gt;](CreateVoucherEntryRequest.md)

## Example

```typescript
import type { UpdateVoucherRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "date": null,
  "attachmentCount": null,
  "entries": null,
} satisfies UpdateVoucherRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as UpdateVoucherRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


