
# ArchiveCategoryDto


## Properties

Name | Type
------------ | -------------
`id` | string
`parentId` | string
`name` | string
`sortOrder` | number
`isSystem` | boolean

## Example

```typescript
import type { ArchiveCategoryDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "parentId": null,
  "name": null,
  "sortOrder": null,
  "isSystem": null,
} satisfies ArchiveCategoryDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ArchiveCategoryDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


