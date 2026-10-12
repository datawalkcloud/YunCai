
# EmployeeDto


## Properties

Name | Type
------------ | -------------
`id` | string
`code` | string
`name` | string
`genderText` | string
`dept` | string
`phone` | string
`email` | string
`idTypeText` | string
`idNo` | string
`subjectAccountId` | string
`subjectText` | string
`statusText` | string
`hireDate` | Date
`remark` | string

## Example

```typescript
import type { EmployeeDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "code": null,
  "name": null,
  "genderText": null,
  "dept": null,
  "phone": null,
  "email": null,
  "idTypeText": null,
  "idNo": null,
  "subjectAccountId": null,
  "subjectText": null,
  "statusText": null,
  "hireDate": null,
  "remark": null,
} satisfies EmployeeDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as EmployeeDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


