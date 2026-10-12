
# CloseBatchResultDto


## Properties

Name | Type
------------ | -------------
`message` | string
`closedPeriods` | Array&lt;string&gt;
`status` | [ClosingStatusDto](ClosingStatusDto.md)

## Example

```typescript
import type { CloseBatchResultDto } from ''

// TODO: Update the object below with actual values
const example = {
  "message": null,
  "closedPeriods": null,
  "status": null,
} satisfies CloseBatchResultDto

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as CloseBatchResultDto
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


