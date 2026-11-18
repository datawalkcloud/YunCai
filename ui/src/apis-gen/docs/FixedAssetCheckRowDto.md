
# FixedAssetCheckRowDto


## Properties

Name | Type
------------ | -------------
`item` | string
`name` | string
`opening` | number
`debit` | number
`credit` | number
`balance` | number
`isDiff` | boolean

## Example

```typescript
import type { FixedAssetCheckRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "item": null,
  "name": null,
  "opening": null,
  "debit": null,
  "credit": null,
  "balance": null,
  "isDiff": null,
} satisfies FixedAssetCheckRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as FixedAssetCheckRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


