
# TaxKindItemDto


## Properties

Name | Type
------------ | -------------
`name` | string
`rate` | string
`validFrom` | string
`validTo` | string
`source` | string

## Example

```typescript
import type { TaxKindItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "rate": null,
  "validFrom": null,
  "validTo": null,
  "source": null,
} satisfies TaxKindItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxKindItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


