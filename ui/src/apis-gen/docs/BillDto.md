
# BillDto


## Properties

Name | Type
------------ | -------------
`id` | string
`category` | string
`issueDate` | Date
`billNo` | string
`amount` | number
`kind` | number
`kindText` | string
`dueDate` | Date
`drawer` | string
`payee` | string
`acceptor` | string
`drawerAccountNo` | string
`payeeAccountNo` | string
`acceptorAccountNo` | string
`drawerBank` | string
`payeeBank` | string
`acceptorBank` | string
`status` | number
`statusText` | string
`transferable` | boolean
`remark` | string
`voucherId` | string
`voucherNo` | string
`attachmentCount` | number

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
  "kind": null,
  "kindText": null,
  "dueDate": null,
  "drawer": null,
  "payee": null,
  "acceptor": null,
  "drawerAccountNo": null,
  "payeeAccountNo": null,
  "acceptorAccountNo": null,
  "drawerBank": null,
  "payeeBank": null,
  "acceptorBank": null,
  "status": null,
  "statusText": null,
  "transferable": null,
  "remark": null,
  "voucherId": null,
  "voucherNo": null,
  "attachmentCount": null,
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


