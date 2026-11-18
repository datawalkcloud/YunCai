
# TaxTaskRowDto


## Properties

Name | Type
------------ | -------------
`key` | string
`period` | number
`title` | string
`status` | string
`suggestion` | string
`submittedAt` | Date
`finishedAt` | Date
`operator` | string

## Example

```typescript
import type { TaxTaskRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "period": null,
  "title": null,
  "status": null,
  "suggestion": null,
  "submittedAt": null,
  "finishedAt": null,
  "operator": null,
} satisfies TaxTaskRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxTaskRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


