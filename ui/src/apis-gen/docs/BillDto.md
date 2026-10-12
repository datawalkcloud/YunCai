
# BillDto


## Properties

Name | Type
------------ | -------------
`id` | string
`category` | string
`issueDate` | Date
`billNo` | string
`amount` | number
`kindText` | string
`dueDate` | Date
`drawer` | string
`acceptor` | string
`statusText` | string
`transferable` | boolean
`remark` | string

## Example

```typescript
import type { BillDto } from ''

// TODO: Update the object below with actual values
const example = {
  "id": null,
  "category": null,
  "issueDate": null,
  "billNo": null,
  "amount": null,
  "kindText": null,
  "dueDate": null,
  "drawer": null,
  "acceptor": null,
  "statusText": null,
  "transferable": null,
  "remark": null,
} satisfies BillDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as BillDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


