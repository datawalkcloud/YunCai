
# TaxDeclarationRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`declareKind` | string
`periodStart` | string
`periodEnd` | string
`status` | string
`payable` | number
`declareDate` | string
`feedback` | string

## Example

```typescript
import type { TaxDeclarationRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "declareKind": null,
  "periodStart": null,
  "periodEnd": null,
  "status": null,
  "payable": null,
  "declareDate": null,
  "feedback": null,
} satisfies TaxDeclarationRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxDeclarationRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


