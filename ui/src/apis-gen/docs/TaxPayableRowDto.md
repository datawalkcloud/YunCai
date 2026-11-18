
# TaxPayableRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`kind` | string
`periodStart` | string
`periodEnd` | string
`deadline` | string
`refund` | number
`payable` | number
`surcharge` | number
`status` | string

## Example

```typescript
import type { TaxPayableRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "kind": null,
  "periodStart": null,
  "periodEnd": null,
  "deadline": null,
  "refund": null,
  "payable": null,
  "surcharge": null,
  "status": null,
} satisfies TaxPayableRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxPayableRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


