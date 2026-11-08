
# CarryForwardPreviewDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`periodText` | string
`alreadyCarried` | boolean
`needsCarry` | boolean
`profitAmount` | number
`retainedProfitAmount` | number
`hasRetainedVoucher` | boolean
`retainedVoucher` | [CarryPreviewVoucherDto](CarryPreviewVoucherDto.md)
`vouchers` | [Array&lt;CarryPreviewVoucherDto&gt;](CarryPreviewVoucherDto.md)
`warnings` | Array&lt;string&gt;

## Example

```typescript
import type { CarryForwardPreviewDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "periodText": null,
  "alreadyCarried": null,
  "needsCarry": null,
  "profitAmount": null,
  "retainedProfitAmount": null,
  "hasRetainedVoucher": null,
  "retainedVoucher": null,
  "vouchers": null,
  "warnings": null,
} satisfies CarryForwardPreviewDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CarryForwardPreviewDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


