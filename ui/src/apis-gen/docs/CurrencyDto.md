
# CurrencyDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`name` | string
`rate` | number
`convertType` | number
`convertText` | string
`isBase` | boolean

## Example

```typescript
import type { CurrencyDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "name": null,
  "rate": null,
  "convertType": null,
  "convertText": null,
  "isBase": null,
} satisfies CurrencyDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CurrencyDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


