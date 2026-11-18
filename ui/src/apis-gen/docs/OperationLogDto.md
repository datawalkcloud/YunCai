
# OperationLogDto


## Properties

Name | Type
------------ | -------------
`id` | string
`userId` | string
`userName` | string
`type` | string
`module` | string
`log` | string
`time` | Date
`entityType` | string
`entityId` | string
`beforeJson` | string
`afterJson` | string
`ipAddress` | string

## Example

```typescript
import type { OperationLogDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "userId": null,
  "userName": null,
  "type": null,
  "module": null,
  "log": null,
  "time": null,
  "entityType": null,
  "entityId": null,
  "beforeJson": null,
  "afterJson": null,
  "ipAddress": null,
} satisfies OperationLogDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as OperationLogDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


