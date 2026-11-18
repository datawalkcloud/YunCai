
# MultiColumnRowDto


## Properties

Name | Type
------------ | -------------
`kind` | number
`date` | string
`voucherNo` | string
`summary` | string
`debits` | Array&lt;number&gt;
`credits` | Array&lt;number&gt;
`direction` | string
`balance` | number

## Example

```typescript
import type { MultiColumnRowDto } from ''

// TODO: Update the object below with actual values
const example = {
  "kind": null,
  "date": null,
  "voucherNo": null,
  "summary": null,
  "debits": null,
  "credits": null,
  "direction": null,
  "balance": null,
} satisfies MultiColumnRowDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as MultiColumnRowDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


