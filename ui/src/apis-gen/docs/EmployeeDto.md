
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
`birthDate` | Date
`bankName` | string
`bankAccountNo` | string
`post` | string
`title` | string
`education` | string
`isDisabled` | boolean
`isMartyrFamily` | boolean
`isSoloElder` | boolean
`isSeniorExpert` | boolean
`employmentTypeText` | string
`deductBasicExpense` | boolean
`payStartDate` | Date
`paysSocialInsurance` | boolean
`insurances` | [Array&lt;EmployeeInsuranceDto&gt;](EmployeeInsuranceDto.md)
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
  "birthDate": null,
  "bankName": null,
  "bankAccountNo": null,
  "post": null,
  "title": null,
  "education": null,
  "isDisabled": null,
  "isMartyrFamily": null,
  "isSoloElder": null,
  "isSeniorExpert": null,
  "employmentTypeText": null,
  "deductBasicExpense": null,
  "payStartDate": null,
  "paysSocialInsurance": null,
  "insurances": null,
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


