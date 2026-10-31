
# CashFlowWorkpaperDto


## Properties

Name | Type
------------ | -------------
`year` | number
`month` | number
`periodText` | string
`rows` | [Array&lt;CashFlowWorkpaperRowDto&gt;](CashFlowWorkpaperRowDto.md)
`inflowTotal` | number
`outflowTotal` | number

## Example

```typescript
import type { CashFlowWorkpaperDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "month": null,
  "periodText": null,
  "rows": null,
  "inflowTotal": null,
  "outflowTotal": null,
} satisfies CashFlowWorkpaperDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashFlowWorkpaperDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


