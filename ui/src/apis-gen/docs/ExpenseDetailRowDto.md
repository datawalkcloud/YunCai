
# ExpenseDetailRowDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`name` | string
`level` | number
`parentId` | string
`hasChildren` | boolean
`current` | number
`total` | number

## Example

```typescript
import type { ExpenseDetailRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "name": null,
  "level": null,
  "parentId": null,
  "hasChildren": null,
  "current": null,
  "total": null,
} satisfies ExpenseDetailRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as ExpenseDetailRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


