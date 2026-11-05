
# IitPersonnelSyncRow


## Properties

Name | Type
------------ | -------------
`name` | string
`code` | string
`idTypeText` | string
`idNo` | string
`genderText` | string
`phone` | string
`dept` | string
`employTypeText` | string
`statusText` | string
`hireDate` | string
`leaveDate` | string

## Example

```typescript
import type { IitPersonnelSyncRow } from ''

// TODO: Update the object below with actual values
const example = {
  "name": null,
  "code": null,
  "idTypeText": null,
  "idNo": null,
  "genderText": null,
  "phone": null,
  "dept": null,
  "employTypeText": null,
  "statusText": null,
  "hireDate": null,
  "leaveDate": null,
} satisfies IitPersonnelSyncRow

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as IitPersonnelSyncRow
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


