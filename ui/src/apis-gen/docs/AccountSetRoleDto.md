
# AccountSetRoleDto


## Properties

Name | Type
------------ | -------------
`id` | string
`name` | string
`isSystem` | boolean
`isAdmin` | boolean
`permissions` | Array&lt;string&gt;
`permissionText` | string
`sortOrder` | number

## Example

```typescript
import type { AccountSetRoleDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "name": null,
  "isSystem": null,
  "isAdmin": null,
  "permissions": null,
  "permissionText": null,
  "sortOrder": null,
} satisfies AccountSetRoleDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as AccountSetRoleDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


