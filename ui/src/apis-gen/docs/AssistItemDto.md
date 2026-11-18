
# AssistItemDto


## Properties

Name | Type
------------ | -------------
`id` | string
`type` | number
`typeText` | string
`code` | string
`name` | string
`parentId` | string
`level` | number
`isLeaf` | boolean
`isEnabled` | boolean
`contact` | string
`remark` | string

## Example

```typescript
import type { AssistItemDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "type": null,
  "typeText": null,
  "code": null,
  "name": null,
  "parentId": null,
  "level": null,
  "isLeaf": null,
  "isEnabled": null,
  "contact": null,
  "remark": null,
} satisfies AssistItemDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AssistItemDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


