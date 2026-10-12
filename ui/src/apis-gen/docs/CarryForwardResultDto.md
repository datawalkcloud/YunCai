
# CarryForwardResultDto


## Properties

Name | Type
------------ | -------------
`periodText` | string
`vouchers` | [Array&lt;CarryVoucherRefDto&gt;](CarryVoucherRefDto.md)
`profitAmount` | number
`message` | string

## Example

```typescript
import type { CarryForwardResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "periodText": null,
  "vouchers": null,
  "profitAmount": null,
  "message": null,
} satisfies CarryForwardResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CarryForwardResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


