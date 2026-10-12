
# SaveEmployeeRequest


## Properties

Name | Type
------------ | -------------
`code` | string
`name` | string
`gender` | number
`idType` | number
`idNo` | string
`dept` | string
`phone` | string
`email` | string
`birthDate` | Date
`hireDate` | Date
`bankName` | string
`bankAccountNo` | string
`post` | string
`title` | string
`subjectAccountId` | string
`status` | number
`remark` | string

## Example

```typescript
import type { SaveEmployeeRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "code": null,
  "name": null,
  "gender": null,
  "idType": null,
  "idNo": null,
  "dept": null,
  "phone": null,
  "email": null,
  "birthDate": null,
  "hireDate": null,
  "bankName": null,
  "bankAccountNo": null,
  "post": null,
  "title": null,
  "subjectAccountId": null,
  "status": null,
  "remark": null,
} satisfies SaveEmployeeRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveEmployeeRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


