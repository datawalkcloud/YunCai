
# OpeningBalanceRowDto


## Properties

Name | Type
------------ | -------------
`accountId` | string
`code` | string
`name` | string
`category` | number
`direction` | number
`level` | number
`isLeaf` | boolean
`openingBalance` | number
`debit` | number
`credit` | number

## Example

```typescript
import type { OpeningBalanceRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "accountId": null,
  "code": null,
  "name": null,
  "category": null,
  "direction": null,
  "level": null,
  "isLeaf": null,
  "openingBalance": null,
  "debit": null,
  "credit": null,
} satisfies OpeningBalanceRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as OpeningBalanceRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


