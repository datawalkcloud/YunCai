
# CashFlowOpeningDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`periodText` | string
`rows` | [Array&lt;CashFlowOpeningRowDto&gt;](CashFlowOpeningRowDto.md)
`openingTotal` | number
`closingTotal` | number

## Example

```typescript
import type { CashFlowOpeningDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "periodText": null,
  "rows": null,
  "openingTotal": null,
  "closingTotal": null,
} satisfies CashFlowOpeningDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashFlowOpeningDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


