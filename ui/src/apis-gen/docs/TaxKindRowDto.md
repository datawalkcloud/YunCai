
# TaxKindRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`project` | string
`declareKind` | string
`period` | string
`option` | string
`validFrom` | string
`validTo` | string
`source` | string
`items` | [Array&lt;TaxKindItemDto&gt;](TaxKindItemDto.md)

## Example

```typescript
import type { TaxKindRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "project": null,
  "declareKind": null,
  "period": null,
  "option": null,
  "validFrom": null,
  "validTo": null,
  "source": null,
  "items": null,
} satisfies TaxKindRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxKindRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


