
# AccountSetMemberDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`phone` | string
`email` | string
`role` | number
`roleText` | string
`permissionText` | string
`enabled` | boolean
`isOwner` | boolean
`createdAt` | Date

## Example

```typescript
import type { AccountSetMemberDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "phone": null,
  "email": null,
  "role": null,
  "roleText": null,
  "permissionText": null,
  "enabled": null,
  "isOwner": null,
  "createdAt": null,
} satisfies AccountSetMemberDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AccountSetMemberDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


