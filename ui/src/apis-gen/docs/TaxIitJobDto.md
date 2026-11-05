
# TaxIitJobDto


## Properties

Name | Type
------------ | -------------
`key` | string
`title` | string
`status` | string
`needLogin` | boolean
`message` | string
`data` | any
`submittedAt` | Date
`finishedAt` | Date

## Example

```typescript
import type { TaxIitJobDto } from ''

// TODO: Update the object below with actual values
const example = {
  "key": null,
  "title": null,
  "status": null,
  "needLogin": null,
  "message": null,
  "data": null,
  "submittedAt": null,
  "finishedAt": null,
} satisfies TaxIitJobDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as TaxIitJobDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


