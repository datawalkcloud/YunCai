
# CarryBatchResultDto


## Properties

Name | Type
------------ | -------------
`message` | string
`markedPeriods` | Array&lt;string&gt;
`status` | [ClosingStatusDto](ClosingStatusDto.md)

## Example

```typescript
import type { CarryBatchResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "message": null,
  "markedPeriods": null,
  "status": null,
} satisfies CarryBatchResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CarryBatchResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


