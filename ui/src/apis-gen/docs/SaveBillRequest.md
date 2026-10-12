
# SaveBillRequest


## Properties

Name | Type
------------ | -------------
`category` | string
`issueDate` | Date
`billNo` | string
`amount` | number
`kind` | number
`dueDate` | Date
`drawer` | string
`acceptor` | string
`transferable` | boolean
`remark` | string

## Example

```typescript
import type { SaveBillRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "category": null,
  "issueDate": null,
  "billNo": null,
  "amount": null,
  "kind": null,
  "dueDate": null,
  "drawer": null,
  "acceptor": null,
  "transferable": null,
  "remark": null,
} satisfies SaveBillRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveBillRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


