
# PreCloseItemDto


## Properties

Name | Type
------------ | -------------
`key` | string
`title` | string
`amount` | number
`available` | boolean
`source` | string
`hint` | string
`generated` | boolean
`voucherLabel` | string
`voucherId` | string

## Example

```typescript
import type { PreCloseItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "title": null,
  "amount": null,
  "available": null,
  "source": null,
  "hint": null,
  "generated": null,
  "voucherLabel": null,
  "voucherId": null,
} satisfies PreCloseItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as PreCloseItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


