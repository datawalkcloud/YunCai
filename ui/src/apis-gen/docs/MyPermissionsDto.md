
# MyPermissionsDto


## Properties

Name | Type
------------ | -------------
`roleId` | string
`roleName` | string
`isAdmin` | boolean
`permissions` | Array&lt;string&gt;
`permissionText` | string

## Example

```typescript
import type { MyPermissionsDto } from ''

// TODO: Update the object below with actual values
const example = {
  "roleId": null,
  "roleName": null,
  "isAdmin": null,
  "permissions": null,
  "permissionText": null,
} satisfies MyPermissionsDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MyPermissionsDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


