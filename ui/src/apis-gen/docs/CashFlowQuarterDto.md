
# CashFlowQuarterDto


## Properties

Name | Type
------------ | -------------
`year` | number
`quarter` | number
`periodText` | string
`lines` | [Array&lt;CashFlowLineDto&gt;](CashFlowLineDto.md)

## Example

```typescript
import type { CashFlowQuarterDto } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "quarter": null,
  "periodText": null,
  "lines": null,
} satisfies CashFlowQuarterDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CashFlowQuarterDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


