
# SaveBudgetRequest


## Properties

Name | Type
------------ | -------------
`year` | number
`name` | string
`note` | string
`lines` | [Array&lt;SaveBudgetLineRequest&gt;](SaveBudgetLineRequest.md)

## Example

```typescript
import type { SaveBudgetRequest } from ''

// TODO: Update the object below with actual values
const example = {
  "year": null,
  "name": null,
  "note": null,
  "lines": null,
} satisfies SaveBudgetRequest

console.log(example)

// Convert the instance to a JSON string
const exampleJSON: string = JSON.stringify(example)
console.log(exampleJSON)

// Parse the JSON string back to an object
const exampleParsed = JSON.parse(exampleJSON) as SaveBudgetRequest
console.log(exampleParsed)
```

[[Back to top]](#) [[Back to API list]](../README.md#api-endpoints) [[Back to Model list]](../README.md#models) [[Back to README]](../README.md)


